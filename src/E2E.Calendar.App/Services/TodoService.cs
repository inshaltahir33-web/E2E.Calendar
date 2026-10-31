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

    /// <summary>Adds a task to the open task list (Inbox or a named list) or a reminder (smart views).</summary>
    public async Task<bool> AddAsync(string title, string? filter)
    {
        title = (title ?? string.Empty).Trim();

        if (title.Length == 0)
        {
            return false;
        }

        var now = _clock.UtcNow;
        var name = Normalize(filter);

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
                Title = title,
            });
        }
        else
        {
            // Parse the text the same way the quick add preview does, so "in 2 minutes"
            // or "at 5pm" keeps its due time instead of a fixed +1 hour.
            var parsed = QuickAddParser.Parse(title, _clock);

            // Keep the parsed day: "in 3 days" / "4 Nov" / "tomorrow" must not collapse to
            // "tomorrow at the current clock time". A day without a time lands at 09:00 local.
            var dueUtc = parsed.HasTime
                ? parsed.StartUtc
                : parsed.HasDate
                    ? _clock.LocalTimeZone.ToUtc(parsed.Date.ToDateTime(new TimeOnly(9, 0)))
                    : name == Scheduled ? now.AddDays(1) : now.AddHours(1);

            await _engine.CreateStandaloneAsync(
                new Reminder
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Title = parsed.Title,
                    DueUtc = dueUtc,
                    TimeZoneId = _clock.LocalTimeZone.ToIanaId(),
                    Flagged = name == Flagged,
                    CreatedUtc = now,
                    UpdatedUtc = now,
                },
                alertOffsetMinutes: parsed.AlertOffsetsMinutes.Count > 0
                    ? parsed.AlertOffsetsMinutes[0]
                    : 0);

            _logger.LogInformation("Reminder created: {Title} due {DueUtc}.", title, dueUtc);
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

        string Describe(DateTimeOffset? due)
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

            return $"{day}, {local:t}";
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
                Describe(task.DueUtc),
                !done && task.DueUtc is { } due && due < now,
                done,
                task.Priority >= Priority.High,
                listName,
                task.ParentId));
        }

        return rows;
    }
}
