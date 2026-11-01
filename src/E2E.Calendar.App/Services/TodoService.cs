using System;
using System.Collections.Generic;
using System.Globalization;
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

/// <summary>A section of the to-do page: a header for Scheduled/Today, null when the page is flat.</summary>
public sealed record TodoGroup(string? Header, IReadOnlyList<TodoRow> Rows);

/// <summary>
/// Single place that answers "what is in Today / Scheduled / Overdue / Completed / Flagged / Inbox / any named list".
/// The to-do page and the sidebar counts both use it, so the number next to a row always matches the list.
/// </summary>
public sealed class TodoService
{
    public const string Today = "Today";
    public const string Next7Days = "Next 7 days";
    public const string Scheduled = "Scheduled";
    public const string NoDate = "No date";
    public const string Overdue = "Overdue";
    public const string Completed = "Completed";
    public const string Flagged = "Flagged";
    public const string Inbox = "Inbox";

    private static readonly string[] SmartFilters =
    {
        Today, Next7Days, Scheduled, NoDate, Overdue, Completed, Flagged, Inbox,
    };

    private readonly IReminderRepository _reminders;
    private readonly ITaskRepository _tasks;
    private readonly IClock _clock;
    private readonly ISettingsService _settings;
    private readonly ReminderEngine _engine;
    private readonly ReminderNotificationService _scheduler;
    private readonly ILogger<TodoService> _logger;

    public TodoService(
        IReminderRepository reminders,
        ITaskRepository tasks,
        IClock clock,
        ISettingsService settings,
        ReminderEngine engine,
        ReminderNotificationService scheduler,
        ILogger<TodoService> logger)
    {
        ArgumentNullException.ThrowIfNull(reminders);
        ArgumentNullException.ThrowIfNull(tasks);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(scheduler);
        ArgumentNullException.ThrowIfNull(logger);

        _reminders = reminders;
        _tasks = tasks;
        _clock = clock;
        _settings = settings;
        _engine = engine;
        _scheduler = scheduler;
        _logger = logger;
    }

    /// <summary>Raised after any add / complete / flag / delete so the page and sidebar counts refresh.</summary>
    public event EventHandler? Changed;

    /// <summary>True for Inbox and every named list; smart views filter reminders instead.</summary>
    public static bool IsTaskList(string? filter) =>
        !string.IsNullOrWhiteSpace(filter)
        && Normalize(filter) is not (Today or Next7Days or Scheduled or NoDate or Overdue or Completed or Flagged);

    /// <summary>Names that cannot be used for a user list because they are built-in views or Inbox.</summary>
    public static bool IsReservedListName(string? name) =>
        !string.IsNullOrWhiteSpace(name)
        && SmartFilters.Contains(name.Trim(), StringComparer.OrdinalIgnoreCase);

    /// <summary>Overdue, Completed, Flagged and the date-window views are read-only: no add box.</summary>
    public static bool CanAdd(string? filter) => Normalize(filter) switch
    {
        Next7Days or NoDate or Overdue or Completed or Flagged => false,
        _ => true,
    };

    /// <summary>Default values for the add row: Today = next full hour (today), Scheduled = tomorrow 09:00.</summary>
    public (DateOnly Date, TimeOnly Time) GetAddDefault(string? filter)
    {
        var local = _clock.LocalTimeZone.ToLocal(_clock.UtcNow);
        var today = DateOnly.FromDateTime(local);

        if (Normalize(filter) == Scheduled)
        {
            return (today.AddDays(1), new TimeOnly(9, 0));
        }

        var nextHour = new DateTime(local.Year, local.Month, local.Day, local.Hour, 0, 0).AddHours(1);

        // Today never moves to tomorrow: near midnight the next full hour would.
        return DateOnly.FromDateTime(nextHour) == today
            ? (today, TimeOnly.FromDateTime(nextHour))
            : (today, new TimeOnly(23, 59));
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

    /// <summary>
    /// The same rows as <see cref="LoadAsync"/>, split for display: Scheduled by day,
    /// Today as Overdue then Later today, every other page as one unheaded section.
    /// </summary>
    public async Task<IReadOnlyList<TodoGroup>> LoadGroupedAsync(string? filter)
    {
        var rows = await LoadAsync(filter);
        var name = Normalize(filter);

        if (name is Scheduled or Next7Days)
        {
            return rows
                .GroupBy(row => LocalDate(row.DueUtc))
                .Select(group => new TodoGroup(
                    FormatDay(group.Key),
                    group.OrderBy(row => row.DueUtc).ToList()))
                .ToList();
        }

        if (name == Today)
        {
            var groups = new List<TodoGroup>();
            var overdue = rows.Where(row => row.IsOverdue).ToList();
            var later = rows.Where(row => !row.IsOverdue).ToList();

            if (overdue.Count > 0)
            {
                groups.Add(new TodoGroup("Overdue", overdue));
            }

            if (later.Count > 0)
            {
                groups.Add(new TodoGroup("Later today", later));
            }

            return groups;
        }

        return rows.Count == 0
            ? Array.Empty<TodoGroup>()
            : new[] { new TodoGroup(null, rows) };
    }

    private DateOnly LocalDate(DateTimeOffset? due) =>
        DateOnly.FromDateTime(_clock.LocalTimeZone.ToLocal(due ?? _clock.UtcNow));

    private string FormatDay(DateOnly day)
    {
        var today = LocalDate(_clock.UtcNow);

        return (day.DayNumber - today.DayNumber) switch
        {
            0 => "Today",
            1 => "Tomorrow",
            _ => day.ToString("ddd d MMM"),
        };
    }

    public async Task<IReadOnlyDictionary<string, int>> GetCountsAsync()
    {
        var all = await LoadAllAsync();
        var lists = await GetListsAsync();
        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var filter in SmartFilters.Concat(lists.Select(list => list.Name)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            counts[filter] = all.Count(row => Matches(filter, row));
        }

        return counts;
    }

    /// <summary>The named lists shown under LISTS. Inbox is virtual and comes first in the sidebar.</summary>
    public async Task<IReadOnlyList<TaskList>> GetListsAsync()
    {
        var lists = await _tasks.GetListsAsync();

        return lists
            .Where(list => !IsReservedListName(list.Name))
            .OrderBy(list => list.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    public async Task<TaskList> CreateListAsync(string name)
    {
        var clean = ValidateListName(name);
        var list = await _tasks.GetOrCreateListAsync(clean);

        Changed?.Invoke(this, EventArgs.Empty);
        return list;
    }

    public async Task RenameListAsync(string id, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        var clean = ValidateListName(name);
        var lists = await _tasks.GetListsAsync();

        if (lists.Any(list => !string.Equals(list.Id, id, StringComparison.Ordinal)
            && string.Equals(list.Name, clean, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException($"A list named “{clean}” already exists.");
        }

        await _tasks.RenameListAsync(id, clean);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Deletes a named list; its tasks fall back to Inbox.</summary>
    public async Task DeleteListAsync(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        await _tasks.DeleteListAsync(id);
        Changed?.Invoke(this, EventArgs.Empty);
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
        TimeOnly? dueTime = null,
        int? repeatEveryMinutes = null)
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
                // Named lists live in task_list; create the row when a stale filter
                // names one that no longer exists.
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
                    : 0,
                repeatEveryMinutes: repeatEveryMinutes is > 0 ? repeatEveryMinutes : null);

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

    /// <summary>The row's due day in the user's time zone, for the row's date picker.</summary>
    public DateOnly? GetLocalDueDate(TodoRow row)
    {
        ArgumentNullException.ThrowIfNull(row);

        return row.DueUtc is { } due
            ? DateOnly.FromDateTime(_clock.LocalTimeZone.ToLocal(due))
            : null;
    }

    /// <summary>Sets or clears a task's due day; task lists set dates from the row, not the add box.</summary>
    public async Task SetDueAsync(TodoRow row, DateOnly? date)
    {
        ArgumentNullException.ThrowIfNull(row);

        if (row.Kind != TodoKind.Task)
        {
            return;
        }

        var task = await _tasks.GetByIdAsync(row.Id);

        if (task is null)
        {
            return;
        }

        await _tasks.UpsertAsync(task with
        {
            DueUtc = date is { } day
                ? _clock.LocalTimeZone.ToUtc(day.ToDateTime(new TimeOnly(9, 0)))
                : null,
            HasTime = false,
        });

        Changed?.Invoke(this, EventArgs.Empty);
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

    private static string ValidateListName(string name)
    {
        var clean = (name ?? string.Empty).Trim();

        if (clean.Length == 0)
        {
            throw new ArgumentException("A list needs a name.", nameof(name));
        }

        if (IsReservedListName(clean))
        {
            throw new InvalidOperationException($"“{clean}” is reserved and cannot be used as a list name.");
        }

        return clean;
    }

    private static string Normalize(string? filter)
    {
        if (string.IsNullOrWhiteSpace(filter))
        {
            return Today;
        }

        var trimmed = filter.Trim();
        var match = SmartFilters.FirstOrDefault(entry =>
            string.Equals(entry, trimmed, StringComparison.OrdinalIgnoreCase));

        return match ?? trimmed;
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

            // Unfinished dated tasks stay in Overdue and also surface on Today, where
            // the row carries an Overdue badge instead of disappearing until completed.
            Today => !row.IsDone && row.DueUtc is { } due
                && (DateOnly.FromDateTime(timeZone.ToLocal(due)) == today
                    || (row.Kind == TodoKind.Task && due < now)),
            Next7Days => !row.IsDone && row.DueUtc is { } due
                && due >= now && due < now.AddDays(7),
            Scheduled => !row.IsDone && row.DueUtc is { } due
                && DateOnly.FromDateTime(timeZone.ToLocal(due)) > today,
            NoDate => !row.IsDone && row.DueUtc is null,
            Overdue => !row.IsDone && row.DueUtc is { } due && due < now,
            _ => !row.IsDone && row.Kind == TodoKind.Task
                && string.Equals(row.ListName, filter, StringComparison.OrdinalIgnoreCase),
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

            var time = _settings.Clock == ClockFormat.TwentyFourHour
                ? local.ToString("HH:mm", CultureInfo.CurrentCulture)
                : local.ToString("h:mm tt", CultureInfo.CurrentCulture);

            return hasTime ? $"{day}, {time}" : day;
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
