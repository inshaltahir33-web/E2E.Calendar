using CommunityToolkit.Mvvm.ComponentModel;
using E2E.Calendar.Core.Abstractions;
using E2E.Calendar.Core.Models;

namespace E2E.Calendar.App.ViewModels;

public sealed partial class TodoViewModel : ObservableObject
{
    private readonly ITaskRepository _tasks;
    private readonly IReminderRepository _reminders;
    private readonly IClock _clock;

    [ObservableProperty]
    private bool _showReminders;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private IReadOnlyList<TodoRow> _items = Array.Empty<TodoRow>();

    [ObservableProperty]
    private string _title = "Tasks";

    private string? _currentFilter;

    public TodoViewModel(ITaskRepository tasks, IReminderRepository reminders, IClock clock)
    {
        _tasks = tasks;
        _reminders = reminders;
        _clock = clock;
    }

    public async Task LoadAsync(string? filter = null)
    {
        IsLoading = true;

        try
        {
            _currentFilter = filter;
            var (kind, listFilter) = ParseFilter(filter);
            ShowReminders = string.Equals(kind, "Reminders", StringComparison.OrdinalIgnoreCase);
            Title = ShowReminders ? "Reminders" : "Tasks";
            if (!string.IsNullOrWhiteSpace(listFilter))
            {
                Title = $"{Title} - {listFilter}";
            }
            var today = DateOnly.FromDateTime(_clock.UtcNow.ToLocalTime().Date);

            if (ShowReminders)
            {
                var reminders = await _reminders.GetInRangeAsync(
                    DateTimeOffset.MinValue,
                    DateTimeOffset.MaxValue);
                Items = reminders
                    .Where(reminder => !reminder.Deleted && MatchesFilter(
                        reminder.DoneUtc,
                        reminder.DueUtc,
                        reminder.Flagged,
                        listFilter,
                        today))
                    .OrderBy(reminder => reminder.DoneUtc is not null)
                    .ThenBy(reminder => reminder.DueUtc ?? DateTimeOffset.MaxValue)
                    .ThenBy(reminder => reminder.Title, StringComparer.CurrentCultureIgnoreCase)
                    .Select(reminder => new TodoRow(
                        reminder.Id,
                        reminder.Title,
                        reminder.DueUtc,
                        reminder.Priority,
                        reminder.DoneUtc is not null,
                        reminder.Flagged,
                        true))
                    .ToList();
            }
            else
            {
                var tasks = await _tasks.GetAllAsync();
                Items = tasks
                    .Where(task => !task.Deleted && MatchesFilter(
                        task.DoneUtc,
                        task.DueUtc,
                        false,
                        listFilter,
                        today))
                    .OrderBy(task => task.DoneUtc is not null)
                    .ThenBy(task => task.DueUtc ?? DateTimeOffset.MaxValue)
                    .ThenBy(task => task.Title, StringComparer.CurrentCultureIgnoreCase)
                    .Select(task => new TodoRow(
                        task.Id,
                        task.Title,
                        task.DueUtc,
                        task.Priority,
                        task.DoneUtc is not null,
                        false,
                        false))
                    .ToList();
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    public async Task ToggleAsync(TodoRow row)
    {
        DateTimeOffset? completedAt = row.IsCompleted ? null : _clock.UtcNow;

        if (row.IsReminder)
        {
            var reminder = await _reminders.GetByIdAsync(row.Id);
            if (reminder is not null)
            {
                await _reminders.UpsertAsync(reminder with
                {
                    DoneUtc = completedAt,
                    UpdatedUtc = _clock.UtcNow,
                });
            }
        }
        else
        {
            var task = await _tasks.GetByIdAsync(row.Id);
            if (task is not null)
            {
                await _tasks.UpsertAsync(task with { DoneUtc = completedAt });
            }
        }

        await LoadAsync(_currentFilter);
    }

    private static (string Kind, string? Filter) ParseFilter(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return ("Tasks", null);
        }

        var separator = value.IndexOf('|');
        return separator < 0
            ? (value, null)
            : (value[..separator], value[(separator + 1)..]);
    }

    private static bool MatchesFilter(
        DateTimeOffset? doneUtc,
        DateTimeOffset? dueUtc,
        bool flagged,
        string? filter,
        DateOnly today)
    {
        return filter?.ToLowerInvariant() switch
        {
            "completed" => doneUtc is not null,
            "overdue" => doneUtc is null && dueUtc is not null &&
                         DateOnly.FromDateTime(dueUtc.Value.LocalDateTime) < today,
            "today" => doneUtc is null && dueUtc is not null &&
                       DateOnly.FromDateTime(dueUtc.Value.LocalDateTime) == today,
            "scheduled" => doneUtc is null && dueUtc is not null && dueUtc.Value >= DateTimeOffset.UtcNow,
            "flagged" => doneUtc is null && flagged,
            _ => doneUtc is null,
        };
    }
}

public sealed record TodoRow(
    string Id,
    string Title,
    DateTimeOffset? DueUtc,
    Priority Priority,
    bool IsCompleted,
    bool IsFlagged,
    bool IsReminder)
{
    public string DueLabel => DueUtc is null
        ? "No due date"
        : DueUtc.Value.ToLocalTime().ToString(
            DueUtc.Value.TimeOfDay == TimeSpan.Zero ? "d" : "g",
            System.Globalization.CultureInfo.CurrentCulture);
}
