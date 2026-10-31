using System.Globalization;
using E2E.Calendar.Core;
using E2E.Calendar.Core.Abstractions;
using E2E.Calendar.Core.Models;
using E2E.Calendar.Core.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;

namespace E2E.Calendar.Platform;

public sealed class ReminderNotificationService : BackgroundService
{
    private static readonly TimeSpan FreshWindow = TimeSpan.FromMinutes(5);
    private readonly IAlertRepository _alerts;
    private readonly IEventRepository _events;
    private readonly IReminderRepository _reminders;
    private readonly ITaskRepository _tasks;
    private readonly IClock _clock;
    private readonly ReminderPlanner _planner;
    private readonly ReminderEngine _engine;
    private readonly ISettingsService _settings;
    private readonly ILogger<ReminderNotificationService> _logger;
    private readonly SemaphoreSlim _wake = new(0, 1);

    public ReminderNotificationService(
        IAlertRepository alerts,
        IEventRepository events,
        IReminderRepository reminders,
        ITaskRepository tasks,
        IClock clock,
        ReminderPlanner planner,
        ReminderEngine engine,
        ISettingsService settings,
        ILogger<ReminderNotificationService> logger)
    {
        _alerts = alerts;
        _events = events;
        _reminders = reminders;
        _tasks = tasks;
        _clock = clock;
        _planner = planner;
        _engine = engine;
        _settings = settings;
        _logger = logger;
    }

    public void Wake() => _wake.ReleaseIfAvailable();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await PlanAndScheduleAsync(stoppingToken);
            await ProcessDueAsync(stoppingToken, catchUp: true);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Reminder planning failed during startup.");
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var next = await _alerts.PeekNextPendingInstanceAsync(stoppingToken);

                _logger.LogInformation(
                    "Reminder tick, next due: {NextDue}.",
                    next is null
                        ? "none"
                        : _clock.LocalTimeZone.ToLocal(next.FireAtUtc)
                            .ToString("ddd d MMM, HH:mm:ss", CultureInfo.InvariantCulture));

                var delay = next is null
                    ? TimeSpan.FromMinutes(1)
                    : next.FireAtUtc - _clock.UtcNow;
                if (delay < TimeSpan.Zero)
                {
                    delay = TimeSpan.Zero;
                }

                // Wait for the next due time or for a wake (an alert was created or
                // edited), capped at a minute. The plan is refreshed every tick so a
                // new alert gets an instance without an app restart; previously the
                // plan only ran when the stale next-due was null or under a minute away,
                // which is why freshly created reminders were never materialised.
                await _wake.WaitAsync(
                    TimeSpan.FromSeconds(Math.Min(delay.TotalSeconds, 60)),
                    stoppingToken);

                await PlanAndScheduleAsync(stoppingToken);
                await ProcessDueAsync(stoppingToken, catchUp: false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Reminder processing loop failed.");
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
                }
                catch (OperationCanceledException)
                {
                }
            }
        }
    }

    private async Task PlanAndScheduleAsync(CancellationToken cancellationToken)
    {
        // Alerts are planned for the next 30 days so recurring reminders and tasks keep
        // materialising instances as they approach. An unpackaged build cannot queue OS
        // toasts (AppNotification has no schedule API), so due instances are always shown
        // by the in-app loop in ExecuteAsync.
        var planned = await _planner.PlanAsync(_clock.UtcNow, TimeSpan.FromDays(30), cancellationToken);

        _logger.LogInformation("Reminder plan: {Planned} instance(s) planned.", planned);
    }

    private async Task ProcessDueAsync(CancellationToken cancellationToken, bool catchUp)
    {
        var due = await _alerts.GetPendingInstancesAsync(_clock.UtcNow, cancellationToken);
        if (due.Count == 0)
        {
            return;
        }

        foreach (var instance in due)
        {
            // On startup, alerts that came due long before the app ran are retired as
            // missed instead of being toasted (past alerts must not toast at startup).
            // A short grace window still fires alerts that came due just before launch.
            // Persistent chains survive this by queueing their next repeat from now.
            if (catchUp && instance.FireAtUtc < _clock.UtcNow - FreshWindow)
            {
                await _alerts.MarkMissedAsync(instance.Id, _clock.UtcNow, cancellationToken);
                await _engine.TryScheduleRepeatAsync(instance, _clock.UtcNow, cancellationToken);
                continue;
            }

            // A repeat that was queued while the user was away must not nag a reminder
            // that has since been completed or deleted.
            if (!await _engine.ShouldFireAsync(instance, cancellationToken))
            {
                await _alerts.MarkDoneAsync(instance.Id, _clock.UtcNow, cancellationToken);
                continue;
            }

            if (!await _alerts.TryMarkFiredAsync(instance.Id, _clock.UtcNow, cancellationToken))
            {
                continue;
            }

            _logger.LogInformation(
                "Reminder fired: {InstanceId} at {FireAtUtc} (catch-up: {CatchUp}).",
                instance.Id,
                instance.FireAtUtc,
                catchUp);

            await _engine.TryScheduleRepeatAsync(instance, _clock.UtcNow, cancellationToken);
            await ShowToastAsync(instance, catchUp ? "Missed reminder" : "Reminder");
        }
    }

    private async Task ShowToastAsync(AlertInstance instance, string heading)
    {
        try
        {
            var (title, timeLabel, meetingUrl) = await GetOwnerSummaryAsync(instance, CancellationToken.None);

            // AppNotificationBuilder is the supported way to attach actions without hand
            // writing toast XML. The body activation opens the app; Snooze and Done carry
            // action + instance id so ToastActivationService can resolve the instance.
            var builder = new AppNotificationBuilder()
                .AddArgument("action", "open")
                .AddArgument("id", instance.Id)
                .SetTag(ToastCleanup.TagFor(instance.Id))
                .SetGroup(ToastCleanup.Group)
                .AddText(heading)
                .AddText(title)
                .AddText(timeLabel);

            var snoozeMinutes = Math.Max(1, _settings.Current.DefaultSnoozeMinutes);
            builder.AddButton(CreateButton($"Snooze {snoozeMinutes} min", "snooze", instance.Id));
            builder.AddButton(CreateButton("Done", "done", instance.Id));

            if (!string.IsNullOrWhiteSpace(meetingUrl))
            {
                builder.AddButton(CreateButton("Join", "join", instance.Id));
            }

            // AppNotificationManager is the supported path without package identity. The
            // legacy ToastNotificationManager requires package identity or a Start menu
            // shortcut whose AppUserModelID matches the process.
            AppNotificationManager.Default.Show(builder.BuildNotification());

            _logger.LogInformation("Reminder toast shown for {InstanceId}.", instance.Id);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Could not display reminder notification for {InstanceId}.", instance.Id);
        }
    }

    private static AppNotificationButton CreateButton(string content, string action, string instanceId)
    {
        var button = new AppNotificationButton(content);
        button.AddArgument("action", action);
        button.AddArgument("id", instanceId);
        return button;
    }

    private async Task<(string Title, string TimeLabel, string? MeetingUrl)> GetOwnerSummaryAsync(
        AlertInstance instance,
        CancellationToken cancellationToken)
    {
        string? title = null;
        string? meetingUrl = null;

        try
        {
            switch (instance.OwnerType)
            {
                case OwnerType.Event:
                    var calendarEvent = await _events.GetByIdAsync(instance.OwnerId, cancellationToken);
                    title = calendarEvent?.Title;
                    meetingUrl = calendarEvent?.MeetingUrl;
                    break;

                case OwnerType.Reminder:
                    title = (await _reminders.GetByIdAsync(instance.OwnerId, cancellationToken))?.Title;
                    break;

                case OwnerType.Task:
                    title = (await _tasks.GetByIdAsync(instance.OwnerId, cancellationToken))?.Title;
                    break;
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not load the title for alert instance {InstanceId}.", instance.Id);
        }

        var local = _clock.LocalTimeZone.ToLocal(instance.OccurrenceUtc);
        var timeLabel = local.ToString("ddd d MMM, h:mm tt", CultureInfo.CurrentCulture);

        return (string.IsNullOrWhiteSpace(title) ? "Reminder" : title, timeLabel, meetingUrl);
    }
}

internal static class SemaphoreSlimExtensions
{
    public static void ReleaseIfAvailable(this SemaphoreSlim semaphore)
    {
        if (semaphore.CurrentCount == 0)
        {
            semaphore.Release();
        }
    }
}
