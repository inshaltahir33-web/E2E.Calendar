using System.Globalization;
using E2E.Calendar.Core;
using E2E.Calendar.Core.Abstractions;
using E2E.Calendar.Core.Models;
using E2E.Calendar.Core.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;

namespace E2E.Calendar.Platform;

public sealed class ReminderNotificationService : BackgroundService
{
    private const int ScheduledToastLimit = 200;
    private static readonly TimeSpan FreshWindow = TimeSpan.FromMinutes(5);
    private readonly IAlertRepository _alerts;
    private readonly IEventRepository _events;
    private readonly IReminderRepository _reminders;
    private readonly ITaskRepository _tasks;
    private readonly IClock _clock;
    private readonly ReminderPlanner _planner;
    private readonly ILogger<ReminderNotificationService> _logger;
    private readonly SemaphoreSlim _wake = new(0, 1);

    public ReminderNotificationService(
        IAlertRepository alerts,
        IEventRepository events,
        IReminderRepository reminders,
        ITaskRepository tasks,
        IClock clock,
        ReminderPlanner planner,
        ILogger<ReminderNotificationService> logger)
    {
        _alerts = alerts;
        _events = events;
        _reminders = reminders;
        _tasks = tasks;
        _clock = clock;
        _planner = planner;
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
                var delay = next is null
                    ? TimeSpan.FromMinutes(1)
                    : next.FireAtUtc - _clock.UtcNow;
                if (delay < TimeSpan.Zero)
                {
                    delay = TimeSpan.Zero;
                }

                var delayTask = Task.Delay(TimeSpan.FromSeconds(Math.Min(delay.TotalSeconds, 60)), stoppingToken);
                var wakeTask = _wake.WaitAsync(stoppingToken);
                await Task.WhenAny(delayTask, wakeTask);
                await ProcessDueAsync(stoppingToken, catchUp: false);

                if (next is null || delay.TotalSeconds <= 60)
                {
                    await PlanAndScheduleAsync(stoppingToken);
                }
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
        // materialising instances as they approach.
        await _planner.PlanAsync(_clock.UtcNow, TimeSpan.FromDays(30), cancellationToken);
        await ScheduleUpcomingToastsAsync(cancellationToken);
    }

    private async Task ScheduleUpcomingToastsAsync(CancellationToken cancellationToken)
    {
        try
        {
            var notifier = ToastNotificationManager.CreateToastNotifier();
            var pending = await _alerts.GetPendingInstancesForSchedulingAsync(ScheduledToastLimit, cancellationToken);

            // Re-adding on every plan run would pile up duplicates, so everything
            // scheduled for this group is dropped first. Toasts for instances that
            // were re-planned, done or deleted therefore disappear automatically.
            ToastCleanup.RemoveScheduledGroup(notifier);

            foreach (var instance in pending)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (instance.FireAtUtc <= _clock.UtcNow ||
                    instance.FireAtUtc > _clock.UtcNow.AddDays(30))
                {
                    continue;
                }

                notifier.AddToSchedule(new ScheduledToastNotification(
                    await CreateToastXmlAsync(instance, "Scheduled reminder", cancellationToken),
                    instance.FireAtUtc.LocalDateTime)
                {
                    Tag = instance.Id,
                    Group = ToastCleanup.Group,
                });
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "OS reminder scheduling is unavailable; in-app fallback remains active.");
        }
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
            if (catchUp && instance.FireAtUtc < _clock.UtcNow - FreshWindow)
            {
                await _alerts.MarkMissedAsync(instance.Id, _clock.UtcNow, cancellationToken);
                continue;
            }

            if (!await _alerts.TryMarkFiredAsync(instance.Id, _clock.UtcNow, cancellationToken))
            {
                continue;
            }

            await ShowToastAsync(instance, catchUp ? "Missed reminder" : "Reminder");
        }
    }

    private async Task ShowToastAsync(AlertInstance instance, string heading)
    {
        try
        {
            var notifier = ToastNotificationManager.CreateToastNotifier();
            notifier.Show(new ToastNotification(
                await CreateToastXmlAsync(instance, heading, CancellationToken.None))
            {
                Tag = instance.Id,
                Group = ToastCleanup.Group,
            });
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Could not display reminder notification for {InstanceId}.", instance.Id);
        }
    }

    private async Task<XmlDocument> CreateToastXmlAsync(
        AlertInstance instance,
        string heading,
        CancellationToken cancellationToken)
    {
        var (title, timeLabel) = await GetOwnerSummaryAsync(instance, cancellationToken);
        var id = System.Security.SecurityElement.Escape(instance.Id);

        var xml = new XmlDocument();
        xml.LoadXml($"""
            <toast launch="action=open&id={id}">
              <visual>
                <binding template="ToastGeneric">
                  <text>{System.Security.SecurityElement.Escape(heading)}</text>
                  <text>{System.Security.SecurityElement.Escape(title)}</text>
                  <text>{System.Security.SecurityElement.Escape(timeLabel)}</text>
                </binding>
              </visual>
              <actions>
                <action content="Snooze 5 min" activationType="background" arguments="action=snooze&id={id}&minutes=5" />
                <action content="Snooze 10 min" activationType="background" arguments="action=snooze&id={id}&minutes=10" />
                <action content="Snooze 30 min" activationType="background" arguments="action=snooze&id={id}&minutes=30" />
                <action content="Snooze 1 h" activationType="background" arguments="action=snooze&id={id}&minutes=60" />
                <action content="Snooze Tomorrow" activationType="background" arguments="action=snooze&id={id}&minutes=tomorrow" />
                <action content="Done" activationType="background" arguments="action=done&id={id}" />
                <action content="Open" activationType="background" arguments="action=open&id={id}" />
              </actions>
            </toast>
            """);
        return xml;
    }

    private async Task<(string Title, string TimeLabel)> GetOwnerSummaryAsync(
        AlertInstance instance,
        CancellationToken cancellationToken)
    {
        string? title = null;

        try
        {
            title = instance.OwnerType switch
            {
                OwnerType.Event => (await _events.GetByIdAsync(instance.OwnerId, cancellationToken))?.Title,
                OwnerType.Reminder => (await _reminders.GetByIdAsync(instance.OwnerId, cancellationToken))?.Title,
                OwnerType.Task => (await _tasks.GetByIdAsync(instance.OwnerId, cancellationToken))?.Title,
                _ => null,
            };
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

        return (string.IsNullOrWhiteSpace(title) ? "Reminder" : title, timeLabel);
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
