using E2E.Calendar.Core.Abstractions;
using E2E.Calendar.Core.Models;

namespace E2E.Calendar.Core.Services;

/// <summary>
/// Returns every item a calendar view can show — events, open reminders and open
/// tasks — for a UTC range. Day, Week and Month share this query so the same
/// date always lists the same items in every view.
/// </summary>
public sealed class CalendarItemQueryService
{
    private readonly EventQueryService _events;
    private readonly IReminderRepository _reminders;
    private readonly ITaskRepository _tasks;

    public CalendarItemQueryService(
        EventQueryService events,
        IReminderRepository reminders,
        ITaskRepository tasks)
    {
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(reminders);
        ArgumentNullException.ThrowIfNull(tasks);

        _events = events;
        _reminders = reminders;
        _tasks = tasks;
    }

    public async Task<IReadOnlyList<CalendarItem>> GetItemsAsync(
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken = default)
    {
        var items = (await _events.GetItemsAsync(fromUtc, toUtc, cancellationToken: cancellationToken)).ToList();

        foreach (var reminder in await _reminders.GetInRangeAsync(fromUtc, toUtc, cancellationToken))
        {
            if (reminder.DoneUtc is null && !reminder.Deleted && reminder.DueUtc is { } due)
            {
                items.Add(new CalendarItem
                {
                    Id = reminder.Id,
                    Title = reminder.Title,
                    StartUtc = due,
                    EndUtc = due.AddMinutes(15),
                    Kind = CalendarItemKind.Reminder,
                    HasTime = true,
                    IsDone = false,
                });
            }
        }

        foreach (var task in await _tasks.GetAllAsync(cancellationToken))
        {
            if (task.DoneUtc is null && !task.Deleted && task.DueUtc is { } due &&
                due >= fromUtc && due < toUtc)
            {
                items.Add(new CalendarItem
                {
                    Id = task.Id,
                    Title = task.Title,
                    StartUtc = due,
                    EndUtc = task.HasTime ? due.AddMinutes(15) : due.AddDays(1),
                    Kind = CalendarItemKind.Task,
                    HasTime = task.HasTime,
                    IsDone = false,
                });
            }
        }

        return items
            .OrderBy(item => item.StartUtc)
            .ThenBy(item => item.Id, StringComparer.Ordinal)
            .ToList();
    }
}
