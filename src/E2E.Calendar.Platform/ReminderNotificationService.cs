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
        await _planner.PlanAsync(_clock.UtcNow, TimeSpan.FromDays(14), cancellationToken);
        ScheduleUpcomingToasts(cancellationToken);
    }

    private void ScheduleUpcomingToasts(CancellationToken cancellationToken)
    {
        try
        {
            var notifier = ToastNotificationManager.CreateToastNotifier();
            var pending = _alerts.GetPendingInstancesForSchedulingAsync(ScheduledToastLimit, cancellationToken)
                .GetAwaiter()
                .GetResult();

            foreach (var instance in pending)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (instance.FireAtUtc <= _clock.UtcNow ||
                    instance.FireAtUtc > _clock.UtcNow.AddDays(14))
                {
                    continue;
                }

                notifier.AddToSchedule(new ScheduledToastNotification(
                    CreateToastXml(instance, "Scheduled reminder"),
                    instance.FireAtUtc.LocalDateTime)
                {
                    Tag = instance.Id,
                    Group = "reminders",
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
            if (!await _alerts.TryMarkFiredAsync(instance.Id, _clock.UtcNow, cancellationToken))
            {
                continue;
            }

            ShowToast(instance, catchUp ? "Missed reminder" : "Reminder");
        }
    }

    private void ShowToast(AlertInstance instance, string heading)
    {
        try
        {
            var notifier = ToastNotificationManager.CreateToastNotifier();
            notifier.Show(new ToastNotification(CreateToastXml(instance, heading))
            {
                Tag = instance.Id,
                Group = "reminders",
            });
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Could not display reminder notification for {InstanceId}.", instance.Id);
        }
    }

    private static XmlDocument CreateToastXml(AlertInstance instance, string heading)
    {
        var xml = new XmlDocument();
        xml.LoadXml($"""
            <toast launch="e2e-calendar://reminder/{instance.Id}">
              <visual>
                <binding template="ToastGeneric">
                  <text>{System.Security.SecurityElement.Escape(heading)}</text>
                  <text>{System.Security.SecurityElement.Escape($"{instance.OwnerType} reminder")}</text>
                </binding>
              </visual>
              <actions>
                <action content="Snooze 5 min" activationType="protocol" arguments="e2e-calendar://snooze/{instance.Id}/5" />
                <action content="Snooze 10 min" activationType="protocol" arguments="e2e-calendar://snooze/{instance.Id}/10" />
                <action content="Snooze 30 min" activationType="protocol" arguments="e2e-calendar://snooze/{instance.Id}/30" />
                <action content="Snooze 1 h" activationType="protocol" arguments="e2e-calendar://snooze/{instance.Id}/60" />
                <action content="Snooze Tomorrow" activationType="protocol" arguments="e2e-calendar://snooze/{instance.Id}/tomorrow" />
                <action content="Done" activationType="protocol" arguments="e2e-calendar://done/{instance.Id}" />
                <action content="Open" activationType="protocol" arguments="e2e-calendar://open/{instance.Id}" />
              </actions>
            </toast>
            """);
        return xml;
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
