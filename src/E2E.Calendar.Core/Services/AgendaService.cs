using E2E.Calendar.Core.Abstractions;
using E2E.Calendar.Core.Models;

namespace E2E.Calendar.Core.Services;

public sealed record AgendaDay(DateOnly Date, string Header, IReadOnlyList<CalendarItem> Items);

public sealed class AgendaService
{
    private readonly EventQueryService _query;
    private readonly IReminderRepository _reminders;
    private readonly ITaskRepository _tasks;
    private readonly IClock _clock;

    public AgendaService(
        EventQueryService query,
        IReminderRepository reminders,
        ITaskRepository tasks,
        IClock clock)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(reminders);
        ArgumentNullException.ThrowIfNull(tasks);
        ArgumentNullException.ThrowIfNull(clock);

        _query = query;
        _reminders = reminders;
        _tasks = tasks;
        _clock = clock;
    }

    public async Task<IReadOnlyList<AgendaDay>> GetDaysAsync(
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        string? calendarId = null,
        CancellationToken cancellationToken = default)
    {
        var items = new List<CalendarItem>();

        items.AddRange(await _query.GetItemsAsync(fromUtc, toUtc, calendarId, cancellationToken));

        foreach (var reminder in await _reminders.GetInRangeAsync(fromUtc, toUtc, cancellationToken))
        {
            if (reminder.DoneUtc is not null || reminder.Deleted || reminder.DueUtc is not { } dueUtc)
            {
                continue;
            }

            items.Add(new CalendarItem
            {
                Id = reminder.Id,
                Title = reminder.Title,
                StartUtc = dueUtc,
                EndUtc = dueUtc,
                Kind = CalendarItemKind.Reminder,
                HasTime = true,
            });
        }

        foreach (var task in await _tasks.GetAllAsync(cancellationToken))
        {
            if (task.DoneUtc is not null || task.Deleted || task.DueUtc is not { } dueUtc ||
                dueUtc < fromUtc || dueUtc >= toUtc)
            {
                continue;
            }

            items.Add(new CalendarItem
            {
                Id = task.Id,
                Title = task.Title,
                StartUtc = dueUtc,
                EndUtc = dueUtc,
                Kind = CalendarItemKind.Task,
                HasTime = task.HasTime,
            });
        }

        var timeZone = _clock.LocalTimeZone;
        var culture = System.Globalization.CultureInfo.CurrentCulture;

        return items
            .GroupBy(item => DateOnly.FromDateTime(timeZone.ToLocal(item.StartUtc)))
            .OrderBy(group => group.Key)
            .Select(group => new AgendaDay(
                group.Key,
                group.Key.ToDateTime(TimeOnly.MinValue).ToString("D", culture),
                group.OrderBy(item => item.StartUtc).ThenBy(item => item.Title, StringComparer.Ordinal).ToList()))
            .ToList();
    }
}
