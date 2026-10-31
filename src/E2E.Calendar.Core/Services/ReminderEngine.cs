using E2E.Calendar.Core.Abstractions;
using E2E.Calendar.Core.Models;

namespace E2E.Calendar.Core.Services;

/// <summary>
/// Reminder lifecycle rules that sit on top of the planner: standalone countdown
/// reminders and persistent (nagging) alerts that repeat until they are stopped.
/// </summary>
public sealed class ReminderEngine
{
    private readonly IAlertRepository _alerts;
    private readonly IReminderRepository _reminders;
    private readonly IEventRepository _events;
    private readonly ITaskRepository _tasks;

    public ReminderEngine(
        IAlertRepository alerts,
        IReminderRepository reminders,
        IEventRepository events,
        ITaskRepository tasks)
    {
        _alerts = alerts;
        _reminders = reminders;
        _events = events;
        _tasks = tasks;
    }

    /// <summary>
    /// Creates the standalone reminder behind "Remind me in N minutes": it is due
    /// once at <paramref name="nowUtc"/> + N and owns a single at-start alert.
    /// </summary>
    public async Task<Reminder> CreateCountdownAsync(
        int minutes,
        DateTimeOffset nowUtc,
        string timeZoneId,
        string? title = null,
        CancellationToken cancellationToken = default)
    {
        if (minutes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(minutes), "A countdown needs at least one minute.");
        }

        var reminder = new Reminder
        {
            Id = Guid.NewGuid().ToString("N"),
            Title = string.IsNullOrWhiteSpace(title) ? $"Reminder in {minutes} min" : title.Trim(),
            DueUtc = nowUtc.AddMinutes(minutes),
            TimeZoneId = timeZoneId,
            CreatedUtc = nowUtc,
            UpdatedUtc = nowUtc,
        };

        await CreateStandaloneAsync(reminder, cancellationToken: cancellationToken);

        return reminder;
    }

    /// <summary>
    /// Stores a standalone reminder together with the alert row the planner needs to
    /// materialise it. A positive <paramref name="repeatEveryMinutes"/> makes it a
    /// persistent reminder that keeps re-firing until it is done or dismissed.
    /// </summary>
    public async Task CreateStandaloneAsync(
        Reminder reminder,
        int alertOffsetMinutes = 0,
        int? repeatEveryMinutes = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reminder);

        await _reminders.UpsertAsync(reminder, cancellationToken);

        var existing = (await _alerts.GetAlertsForOwnerAsync(
            OwnerType.Reminder,
            reminder.Id,
            cancellationToken)).FirstOrDefault();

        await _alerts.UpsertAlertAsync(
            new Alert
            {
                Id = existing?.Id ?? $"alert-{reminder.Id}",
                OwnerType = OwnerType.Reminder,
                OwnerId = reminder.Id,
                TriggerOffsetMinutes = alertOffsetMinutes,
                TriggerAtUtc = existing?.TriggerAtUtc,
                Priority = reminder.Priority,
                Persistent = repeatEveryMinutes is > 0,
                RepeatEveryMinutes = repeatEveryMinutes is > 0 ? repeatEveryMinutes : null,
                Sound = existing?.Sound,
            },
            cancellationToken);
    }

    public static string RepeatInstanceId(string alertId, DateTimeOffset occurrenceUtc, int repeatCount) =>
        $"{alertId}:{occurrenceUtc.UtcTicks}:repeat:{repeatCount}";

    /// <summary>
    /// Queues the next nagging instance for an alert that just fired or was caught up.
    /// The row is persisted, so the chain survives restarts; the next repeat lands at
    /// least one interval after <paramref name="nowUtc"/> so a missed catch-up cannot
    /// re-toast in a hot loop.
    /// </summary>
    public async Task<bool> TryScheduleRepeatAsync(
        AlertInstance firedInstance,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(firedInstance);

        var alert = await _alerts.GetAlertByIdAsync(firedInstance.AlertId, cancellationToken);

        // The interval is what makes an alert persistent; setting it is enough to opt in.
        if (alert?.RepeatEveryMinutes is not { } interval || interval <= 0)
        {
            return false;
        }

        if (!await IsOwnerActiveAsync(firedInstance.OwnerType, firedInstance.OwnerId, cancellationToken))
        {
            return false;
        }

        var baseTime = firedInstance.FireAtUtc > nowUtc ? firedInstance.FireAtUtc : nowUtc;

        await _alerts.UpsertInstanceAsync(
            new AlertInstance
            {
                Id = RepeatInstanceId(
                    firedInstance.AlertId,
                    firedInstance.OccurrenceUtc,
                    firedInstance.RepeatCount + 1),
                AlertId = firedInstance.AlertId,
                OwnerType = firedInstance.OwnerType,
                OwnerId = firedInstance.OwnerId,
                OccurrenceUtc = firedInstance.OccurrenceUtc,
                FireAtUtc = baseTime.AddMinutes(interval),
                State = AlertState.Pending,
                RepeatCount = firedInstance.RepeatCount + 1,
            },
            cancellationToken);

        return true;
    }

    /// <summary>
    /// False once a repeating alert's owner was completed or deleted, so a repeat
    /// that is already queued can never toast after the user is done with it.
    /// </summary>
    public async Task<bool> ShouldFireAsync(
        AlertInstance instance,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(instance);

        if (instance.RepeatCount == 0)
        {
            return true;
        }

        return await IsOwnerActiveAsync(instance.OwnerType, instance.OwnerId, cancellationToken);
    }

    /// <summary>Removes every repeat that is still queued for this alert occurrence.</summary>
    public Task StopRepeatChainAsync(
        AlertInstance instance,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(instance);

        return _alerts.CancelPendingRepeatsAsync(
            instance.AlertId,
            instance.OccurrenceUtc,
            cancellationToken);
    }

    private async Task<bool> IsOwnerActiveAsync(
        OwnerType ownerType,
        string ownerId,
        CancellationToken cancellationToken) =>
        ownerType switch
        {
            OwnerType.Reminder =>
                await _reminders.GetByIdAsync(ownerId, cancellationToken) is { Deleted: false, DoneUtc: null },
            OwnerType.Task =>
                await _tasks.GetByIdAsync(ownerId, cancellationToken) is { Deleted: false, DoneUtc: null },
            OwnerType.Event =>
                await _events.GetByIdAsync(ownerId, cancellationToken) is { Deleted: false },
            _ => false,
        };
}
