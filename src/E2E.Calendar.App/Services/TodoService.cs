using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using E2E.Calendar.Core;
using E2E.Calendar.Core.Abstractions;
using E2E.Calendar.Core.Models;
using E2E.Calendar.Core.QuickAdd;
using E2E.Calendar.Core.Services;
using E2E.Calendar.Platform;
using Microsoft.Extensions.Logging;

namespace E2E.Calendar.App.Services;

public enum TodoKind
{
    Reminder,
    Task,
}

/// <summary>One line in the to-do page: either a reminder or a task.</summary>
public sealed record TodoRow(
    string Id,
    TodoKind Kind,
    string Title,
    DateTimeOffset? DueUtc,
    string DueText,
    bool IsOverdue,
    bool IsDone,
    bool IsFlagged,
    string ListName,
    string? ParentId = null);

/// <summary>What the add box read from the typed shortcut: the cleaned title and the day/clock it named.</summary>
public sealed record TodoAddPreview(string Title, DateOnly? Date, TimeOnly? Time);

/// <summary>
/// Single place that answers "what is in Today / Scheduled / Overdue / Completed / Flagged / Inbox / Work".
/// The to-do page and the sidebar counts both use it, so the number next to a row always matches the list.
/// </summary>
public sealed class TodoService
{
    public const string Today = "Today";
    public const string Scheduled = "Scheduled";
    public const string Overdue = "Overdue";
    public const string Completed = "Completed";
    public const string Flagged = "Flagged";
    public const string Inbox = "Inbox";
    public const string Work = "Work";

    private static readonly string[] AllFilters = { Today, Scheduled, Overdue, Completed, Flagged, Inbox, Work };

    private readonly IReminderRepository _reminders;
    private readonly ITaskRepository _tasks;
    private readonly IClock _clock;
    private readonly ReminderEngine _engine;
    private readonly ReminderNotificationService _scheduler;
    private readonly ILogger<TodoService> _logger;

    public TodoService(
        IReminderRepository reminders,
        ITaskRepository tasks,
        IClock clock,
        ReminderEngine engine,
        ReminderNotificationService scheduler,
        ILogger<TodoService> logger)
    {
        ArgumentNullException.ThrowIfNull(reminders);
        ArgumentNullException.ThrowIfNull(tasks);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(scheduler);
        ArgumentNullException.ThrowIfNull(logger);

        _reminders = reminders;
        _tasks = tasks;
        _clock = clock;
        _engine = engine;
        _scheduler = scheduler;
        _logger = logger;
    }

    /// <summary>Raised after any add / complete / flag / delete so the page and sidebar counts refresh.</summary>
    public event EventHandler? Changed;

    public static bool IsTaskList(string? filter) =>
        string.Equals(filter, Inbox, StringComparison.OrdinalIgnoreCase)
        || string.Equals(filter, Work, StringComparison.OrdinalIgnoreCase);

    /// <summary>Overdue, Completed and Flagged are read-only views: no add box.</summary>
    public static bool CanAdd(string? filter) => Normalize(filter) switch
    {
        Overdue or Completed or Flagged => false,
        _ => true,
    };

    /// <summary>Default chips for a reminder list: Today = next full hour, Scheduled = tomorrow 09:00.</summary>
    public (DateOnly Date, TimeOnly Time) GetAddDefault(string? filter)
    {
        var local = _clock.LocalTimeZone.ToLocal(_clock.UtcNow);

        if (Normalize(filter) == Scheduled)
        {
            return (DateOnly.FromDateTime(local).AddDays(1), new TimeOnly(9, 0));
        }

        var nextHour = new DateTime(local.Year, local.Month, local.Day, local.Hour, 0, 0).AddHours(1);

        return (DateOnly.FromDateTime(nextHour), TimeOnly.FromDateTime(nextHour));
    }

    /// <summary>
    /// Turns typed text into the add-box chips: the title without the date/time words,
    /// plus the day and clock those words named. A null day means nothing was recognised.
    /// A bare "3 days" (no "in") names no day, so the chips must keep their current values.
    /// </summary>
    public TodoAddPreview Preview(string? text)
    {
        var trimmed = (text ?? string.Empty).Trim();

        if (trimmed.Length == 0)
        {
            return new TodoAddPreview(string.Empty, null, null);
        }

        var parsed = QuickAddParser.Parse(trimmed, _clock);
        var defaultTime = new TimeOnly(9, 0);

        // A clock only counts when the words actually named one. The parser also
        // returns 09:00 for a reminder with no time ("remind me to call mom").
        var hasClock = parsed.StartTime is { } start
            && !parsed.AllDay
            && (start != defaultTime
                || string.Equals(parsed.Title, "Untitled", StringComparison.OrdinalIgnoreCase));

        var title = string.IsNullOrWhiteSpace(parsed.Title) ? trimmed : parsed.Title;

        if (!parsed.HasDate && !hasClock)
        {
            return new TodoAddPreview(title, null, null);
        }

        return new TodoAddPreview(
            title,
            parsed.Date,
            parsed.HasDate ? parsed.StartTime ?? defaultTime : parsed.StartTime);
    }

    public async Task<IReadOnlyList<TodoRow>> LoadAsync(string? filter)
    {
        var all = await LoadAllAsync();
        var name = Normalize(filter);

        var rows = all.Where(row => Matches(name, row)).ToList();

        IEnumerable<TodoRow> ordered = name == Completed
            ? rows.OrderByDescending(row => row.DueUtc ?? DateTimeOffset.MinValue)
            : rows.OrderBy(row => row.DueUtc ?? DateTimeOffset.MaxValue).ThenBy(row => row.Title);

        return ordered.ToList();
    }

    public async Task<IReadOnlyDictionary<string, int>> GetCountsAsync()
    {
        var all = await LoadAllAsync();
        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var filter in AllFilters)
        {
            counts[filter] = all.Count(row => Matches(filter, row));
        }

        return counts;
    }

    /// <summary>
    /// Adds a task to the open task list (Inbox or a named list) or a reminder (smart views).
    /// The chips supply the day and clock; when they are empty the typed words are parsed instead.
    /// </summary>
    public async Task<bool> AddAsync(
        string text,
        string? filter,
        string? title = null,
        DateOnly? dueDate = null,
        TimeOnly? dueTime = null)
    {
        var raw = (text ?? string.Empty).Trim();
        var name = Normalize(filter);
        var parsed = raw.Length == 0 ? null : QuickAddParser.Parse(raw, _clock);
        var cleanTitle = (title ?? parsed?.Title ?? raw).Trim();

        if (cleanTitle.Length == 0)
        {
            return false;
        }

        var now = _clock.UtcNow;

        if (IsTaskList(name))
        {
            string? listId = null;

            if (!string.Equals(name, Inbox, StringComparison.OrdinalIgnoreCase))
            {
                // The sidebar lists are not seeded into task_list, so create the row
                // on first use; otherwise adding to "Work" would find no list and fail.
                listId = (await _tasks.GetOrCreateListAsync(name)).Id;
            }

            await _tasks.UpsertAsync(new TaskItem
            {
                Id = Guid.NewGuid().ToString("N"),
                ListId = listId,
                Title = cleanTitle,
                DueUtc = dueDate is { } taskDate
                    ? _clock.LocalTimeZone.ToUtc(taskDate.ToDateTime(dueTime ?? new TimeOnly(9, 0)))
                    : null,
                HasTime = dueDate is not null && dueTime is not null,
            });
        }
        else
        {
            DateTimeOffset dueUtc;

            if (dueDate is { } reminderDate)
            {
                dueUtc = _clock.LocalTimeZone.ToUtc(reminderDate.ToDateTime(dueTime ?? new TimeOnly(9, 0)));
            }
            else if (parsed is { HasTime: true })
            {
                // Parsed times are UTC already. A day without a time landed on 09:00 local
                // in the parser, so "in 3 days" / "4 Nov" keep their day.
                dueUtc = parsed.StartUtc;
            }
            else if (parsed is { HasDate: true })
            {
                dueUtc = _clock.LocalTimeZone.ToUtc(parsed.Date.ToDateTime(new TimeOnly(9, 0)));
            }
            else
            {
                dueUtc = name == Scheduled ? now.AddDays(1) : now.AddHours(1);
            }

            await _engine.CreateStandaloneAsync(
                new Reminder
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Title = cleanTitle,
                    DueUtc = dueUtc,
                    TimeZoneId = _clock.LocalTimeZone.ToIanaId(),
                    Flagged = name == Flagged,
                    CreatedUtc = now,
                    UpdatedUtc = now,
                },
                alertOffsetMinutes: parsed is { } item && item.AlertOffsetsMinutes.Count > 0
                    ? item.AlertOffsetsMinutes[0]
                    : 0);

            _logger.LogInformation("Reminder created: {Title} due {DueUtc}.", cleanTitle, dueUtc);
            _scheduler.Wake();
        }

        Changed?.Invoke(this, EventArgs.Empty);
        return true;
    }

    public async Task<bool> AddSubtaskAsync(TodoRow parent, string title)
    {
        ArgumentNullException.ThrowIfNull(parent);

        if (parent.Kind != TodoKind.Task || string.IsNullOrWhiteSpace(title))
        {
            return false;
        }

        var task = await _tasks.GetByIdAsync(parent.Id)
            ?? throw new KeyNotFoundException($"Task '{parent.Id}' does not exist.");

        await _tasks.UpsertAsync(new TaskItem
        {
            Id = Guid.NewGuid().ToString("N"),
            ListId = task.ListId,
            ParentId = task.Id,
            Title = title.Trim(),
            Dirty = true,
        });

        Changed?.Invoke(this, EventArgs.Empty);
        return true;
    }

    public async Task SetDoneAsync(TodoRow row, bool done)
    {
        ArgumentNullException.ThrowIfNull(row);
        var now = _clock.UtcNow;

        if (row.Kind == TodoKind.Reminder)
        {
            var reminder = await _reminders.GetByIdAsync(row.Id);

            if (reminder is null)
            {
                return;
            }

            await _reminders.UpsertAsync(reminder with { DoneUtc = done ? now : null, UpdatedUtc = now });
        }
        else
        {
            var task = await _tasks.GetByIdAsync(row.Id);

            if (task is null)
            {
                return;
            }

            await _tasks.UpsertAsync(task with { DoneUtc = done ? now : null });
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Flags a reminder (tasks have no flag field; they use priority).</summary>
    public async Task ToggleFlagAsync(TodoRow row)
    {
        ArgumentNullException.ThrowIfNull(row);

        if (row.Kind == TodoKind.Reminder)
        {
            var reminder = await _reminders.GetByIdAsync(row.Id);

            if (reminder is null)
            {
                return;
            }

            await _reminders.UpsertAsync(reminder with { Flagged = !reminder.Flagged, UpdatedUtc = _clock.UtcNow });
        }
        else
        {
            var task = await _tasks.GetByIdAsync(row.Id);

            if (task is null)
            {
                return;
            }

            var next = task.Priority >= Priority.High ? Priority.Normal : Priority.High;
            await _tasks.UpsertAsync(task with { Priority = next });
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    public async Task DeleteAsync(TodoRow row)
    {
        ArgumentNullException.ThrowIfNull(row);

        if (row.Kind == TodoKind.Reminder)
        {
            var reminder = await _reminders.GetByIdAsync(row.Id);

            if (reminder is null)
            {
                return;
            }

            await _reminders.UpsertAsync(reminder with { Deleted = true, UpdatedUtc = _clock.UtcNow });
        }
        else
        {
            var task = await _tasks.GetByIdAsync(row.Id);

            if (task is null)
            {
                return;
            }

            await _tasks.UpsertAsync(task with { Deleted = true });
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    private static string Normalize(string? filter)
    {
        if (string.IsNullOrWhiteSpace(filter))
        {
            return Today;
        }

        var match = AllFilters.FirstOrDefault(entry =>
            string.Equals(entry, filter.Trim(), StringComparison.OrdinalIgnoreCase));

        return match ?? Today;
    }

    private bool Matches(string filter, TodoRow row)
    {
        var now = _clock.UtcNow;
        var timeZone = _clock.LocalTimeZone;
        var today = DateOnly.FromDateTime(timeZone.ToLocal(now));

        return filter switch
        {
            Completed => row.IsDone,
            Flagged => !row.IsDone && row.IsFlagged,
            Today => !row.IsDone && row.DueUtc is { } due
                && DateOnly.FromDateTime(timeZone.ToLocal(due)) == today,
            Scheduled => !row.IsDone && row.DueUtc is { } due
                && DateOnly.FromDateTime(timeZone.ToLocal(due)) > today,
            Overdue => !row.IsDone && row.DueUtc is { } due && due < now,
            Inbox or Work => !row.IsDone && row.Kind == TodoKind.Task
                && string.Equals(row.ListName, filter, StringComparison.OrdinalIgnoreCase),
            _ => false,
        };
    }

    private async Task<List<TodoRow>> LoadAllAsync()
    {
        var now = _clock.UtcNow;
        var timeZone = _clock.LocalTimeZone;

        var reminders = await _reminders.GetInRangeAsync(now.AddYears(-20), now.AddYears(20));
        var tasks = await _tasks.GetAllAsync();
        var lists = await _tasks.GetListsAsync();
        var listNames = lists.ToDictionary(list => list.Id, list => list.Name);

        string Describe(DateTimeOffset? due, bool hasTime = true)
        {
            if (due is null)
            {
                return string.Empty;
            }

            var local = timeZone.ToLocal(due.Value);
            var today = DateOnly.FromDateTime(timeZone.ToLocal(now));
            var daysAway = DateOnly.FromDateTime(local).DayNumber - today.DayNumber;

            var day = daysAway switch
            {
                0 => "Today",
                1 => "Tomorrow",
                _ => local.ToString("ddd d MMM"),
            };

            return hasTime ? $"{day}, {local:t}" : day;
        }

        var rows = new List<TodoRow>();

        foreach (var reminder in reminders.Where(entry => !entry.Deleted))
        {
            var done = reminder.DoneUtc is not null;
            var listName = reminder.ListId is not null && listNames.TryGetValue(reminder.ListId, out var named)
                ? named
                : "Reminders";

            rows.Add(new TodoRow(
                reminder.Id,
                TodoKind.Reminder,
                reminder.Title,
                reminder.DueUtc,
                Describe(reminder.DueUtc),
                !done && reminder.DueUtc is { } due && due < now,
                done,
                reminder.Flagged,
                listName,
                null));
        }

        foreach (var task in tasks.Where(entry => !entry.Deleted))
        {
            var done = task.DoneUtc is not null;
            var listName = task.ListId is not null && listNames.TryGetValue(task.ListId, out var named)
                ? named
                : Inbox;

            rows.Add(new TodoRow(
                task.Id,
                TodoKind.Task,
                task.Title,
                task.DueUtc,
                Describe(task.DueUtc, task.HasTime),
                !done && task.DueUtc is { } due && due < now,
                done,
                task.Priority >= Priority.High,
                listName,
                task.ParentId));
        }

        return rows;
    }
}
