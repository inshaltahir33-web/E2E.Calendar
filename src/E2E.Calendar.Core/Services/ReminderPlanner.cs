using E2E.Calendar.Core.Abstractions;
using E2E.Calendar.Core.Models;

namespace E2E.Calendar.Core.Services;

public sealed class ReminderPlanner
{
    private readonly IAlertRepository _alerts;
    private readonly IEventRepository _events;
    private readonly IReminderRepository _reminders;
    private readonly ITaskRepository _tasks;
    private readonly IRecurrenceExpander _recurrence;

    public ReminderPlanner(
        IAlertRepository alerts,
        IEventRepository events,
        IReminderRepository reminders,
        ITaskRepository tasks,
        IRecurrenceExpander recurrence)
    {
        _alerts = alerts;
        _events = events;
        _reminders = reminders;
        _tasks = tasks;
        _recurrence = recurrence;
    }

    public async Task<int> PlanAsync(
        DateTimeOffset nowUtc,
        TimeSpan horizon,
        CancellationToken cancellationToken = default)
    {
        var from = nowUtc.AddDays(-1);
        var to = nowUtc.Add(horizon);
        var planned = 0;

        foreach (var alert in await _alerts.GetAllAlertsAsync(cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();

            foreach (var occurrence in await GetOccurrencesAsync(alert, from, to, cancellationToken))
            {
                var fireAt = alert.TriggerAtUtc
                    ?? occurrence.StartUtc - TimeSpan.FromMinutes(alert.TriggerOffsetMinutes ?? 0);

                if (fireAt > to || occurrence.EndUtc < from)
                {
                    continue;
                }

                await _alerts.UpsertInstanceAsync(
                    new AlertInstance
                    {
                        Id = InstanceId(alert.Id, occurrence.StartUtc),
                        AlertId = alert.Id,
                        OwnerType = alert.OwnerType,
                        OwnerId = alert.OwnerId,
                        OccurrenceUtc = occurrence.StartUtc,
                        FireAtUtc = fireAt,
                        State = AlertState.Pending,
                    },
                    cancellationToken);
                planned++;
            }
        }

        return planned;
    }

    private async Task<IReadOnlyList<Occurrence>> GetOccurrencesAsync(
        Alert alert,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken)
    {
        switch (alert.OwnerType)
        {
            case OwnerType.Event:
            {
                var calendarEvent = await _events.GetByIdAsync(alert.OwnerId, cancellationToken);
                if (calendarEvent is null || calendarEvent.Deleted)
                {
                    return [];
                }

                if (string.IsNullOrWhiteSpace(calendarEvent.RRule))
                {
                    return [new Occurrence
                    {
                        EventId = calendarEvent.Id,
                        StartUtc = calendarEvent.StartUtc,
                        EndUtc = calendarEvent.EndUtc,
                    }];
                }

                var exceptions = await _events.GetExceptionsAsync(calendarEvent.Id, from, to, cancellationToken);
                return _recurrence.Expand(calendarEvent, exceptions, from, to).ToArray();
            }
            case OwnerType.Reminder:
            {
                var reminder = await _reminders.GetByIdAsync(alert.OwnerId, cancellationToken);
                return reminder?.DueUtc is { } due && !reminder.Deleted && reminder.DoneUtc is null
                    ? [new Occurrence { EventId = reminder.Id, StartUtc = due, EndUtc = due }]
                    : [];
            }
            case OwnerType.Task:
            {
                var task = await _tasks.GetByIdAsync(alert.OwnerId, cancellationToken);
                return task?.DueUtc is { } due && !task.Deleted && task.DoneUtc is null
                    ? [new Occurrence { EventId = task.Id, StartUtc = due, EndUtc = due }]
                    : [];
            }
            default:
                return [];
        }
    }

    public static string InstanceId(string alertId, DateTimeOffset occurrenceUtc) =>
        $"{alertId}:{occurrenceUtc.UtcTicks}";
}
