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

            var plannedInstanceIds = new List<string>();

            foreach (var occurrence in await GetOccurrencesAsync(alert, from, to, cancellationToken))
            {
                var fireAt = alert.TriggerAtUtc
                    ?? occurrence.StartUtc - TimeSpan.FromMinutes(alert.TriggerOffsetMinutes ?? 0);

                if (fireAt > to || occurrence.EndUtc < from)
                {
                    continue;
                }

                // Alerts whose trigger time is already past are never (re-)planned, so a
                // fresh app start cannot toast reminders that came due while it was closed.
                if (fireAt <= nowUtc)
                {
                    continue;
                }

                var instanceId = InstanceId(alert.Id, occurrence.StartUtc);
                plannedInstanceIds.Add(instanceId);

                await _alerts.UpsertInstanceAsync(
                    new AlertInstance
                    {
                        Id = instanceId,
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

            // Re-plan on change: pending instances that no longer match what the item's
            // current rule/occurrences produce (edited or deleted item, changed rule or
            // alert) are removed so they can never fire.
            await _alerts.DeleteStalePendingInstancesAsync(alert.Id, plannedInstanceIds, cancellationToken);
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
                if (reminder is null || reminder.Deleted || reminder.DoneUtc is not null ||
                    reminder.DueUtc is not { } due)
                {
                    return [];
                }

                if (string.IsNullOrWhiteSpace(reminder.RRule))
                {
                    return [new Occurrence { EventId = reminder.Id, StartUtc = due, EndUtc = due }];
                }

                // Recurring reminders reuse the event recurrence expansion, recurring on
                // their due time in the reminder's own time zone.
                return _recurrence
                    .Expand(ToRecurrenceMaster(reminder.Id, reminder.Title, due, reminder.TimeZoneId, reminder.RRule), [], from, to)
                    .ToArray();
            }
            case OwnerType.Task:
            {
                var task = await _tasks.GetByIdAsync(alert.OwnerId, cancellationToken);
                if (task is null || task.Deleted || task.DoneUtc is not null || task.DueUtc is not { } due)
                {
                    return [];
                }

                if (string.IsNullOrWhiteSpace(task.RRule))
                {
                    return [new Occurrence { EventId = task.Id, StartUtc = due, EndUtc = due }];
                }

                // Tasks carry no time zone of their own; the expander resolves empty
                // zones to UTC, matching how task due times are stored.
                return _recurrence
                    .Expand(ToRecurrenceMaster(task.Id, task.Title, due, string.Empty, task.RRule), [], from, to)
                    .ToArray();
            }
            default:
                return [];
        }
    }

    private static CalendarEvent ToRecurrenceMaster(
        string id,
        string title,
        DateTimeOffset dueUtc,
        string? timeZoneId,
        string rrule)
    {
        return new CalendarEvent
        {
            Id = id,
            CalendarId = string.Empty,
            Title = title,
            StartUtc = dueUtc,
            EndUtc = dueUtc,
            TimeZoneId = timeZoneId ?? string.Empty,
            RRule = rrule,
            CreatedUtc = dueUtc,
            UpdatedUtc = dueUtc,
        };
    }

    public static string InstanceId(string alertId, DateTimeOffset occurrenceUtc) =>
        $"{alertId}:{occurrenceUtc.UtcTicks}";
}
