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
    private readonly FocusAssistService _focusAssist;
    private readonly ILogger<ReminderNotificationService> _logger;
    private readonly SemaphoreSlim _wake = new(0, 1);

    // Planning and firing run from the tick loop and from wake-from-sleep catch-up;
    // serialising them keeps one pass from toasting instances the other just retired.
    private readonly SemaphoreSlim _processing = new(1, 1);
    private CancellationToken _stoppingToken = CancellationToken.None;
    private volatile bool _resumePending;

    public ReminderNotificationService(
        IAlertRepository alerts,
        IEventRepository events,
        IReminderRepository reminders,
        ITaskRepository tasks,
        IClock clock,
        ReminderPlanner planner,
        ReminderEngine engine,
        ISettingsService settings,
        FocusAssistService focusAssist,
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
        _focusAssist = focusAssist;
        _logger = logger;
    }

    public void Wake() => _wake.ReleaseIfAvailable();

    /// <summary>
    /// Wake-from-sleep catch-up: retire everything that came due while the PC slept and
    /// show one summary toast instead of one toast per item, then re-plan immediately.
    /// The pending flag lets the tick loop run the same catch-up if it wins the race for
    /// the processing gate, so the backlog is never toasted item by item.
    /// </summary>
    public async Task ResumeCatchUpAsync()
    {
        _resumePending = true;
        var cancellationToken = _stoppingToken;

        try
        {
            await _processing.WaitAsync(cancellationToken);
            try
            {
                if (_resumePending)
                {
                    _resumePending = false;
                    await CatchUpAfterResumeAsync(cancellationToken);
                }
            }
            finally
            {
                _processing.Release();
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Reminder catch-up after resume failed.");
        }
    }

    private async Task CatchUpAfterResumeAsync(CancellationToken cancellationToken)
    {
        // Retire due rows before planning: the planner deletes pending instances whose
        // fire time fell outside its catch-up window, and after sleep those are exactly
        // the missed reminders the summary has to count.
        await ProcessDueAsync(cancellationToken, DuePass.Resume);
        await PlanAndScheduleAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _stoppingToken = stoppingToken;

        try
        {
            await _processing.WaitAsync(stoppingToken);
            try
            {
                await PlanAndScheduleAsync(stoppingToken);
                await ProcessDueAsync(stoppingToken, DuePass.Startup);
            }
            finally
            {
                _processing.Release();
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Reminder planning failed during startup.");
        }

        var deferred = false;

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

                // A deferred instance is still due, so without this floor the loop would
                // wake immediately and re-check it in a hot spin for as long as Do Not
                // Disturb is on. A wake (new or edited alert) still cuts the wait short.
                if (deferred)
                {
                    delay = TimeSpan.FromSeconds(60);
                }

                // Wait for the next due time or for a wake (an alert was created or
                // edited), capped at a minute. The plan is refreshed every tick so a
                // new alert gets an instance without an app restart; previously the
                // plan only ran when the stale next-due was null or under a minute away,
                // which is why freshly created reminders were never materialised.
                await _wake.WaitAsync(
                    TimeSpan.FromSeconds(Math.Min(delay.TotalSeconds, 60)),
                    stoppingToken);

                await _processing.WaitAsync(stoppingToken);
                try
                {
                    if (_resumePending)
                    {
                        _resumePending = false;
                        await CatchUpAfterResumeAsync(stoppingToken);
                    }
                    else
                    {
                        await PlanAndScheduleAsync(stoppingToken);
                        deferred = await ProcessDueAsync(stoppingToken, DuePass.Normal);
                    }
                }
                finally
                {
                    _processing.Release();
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
        // materialising instances as they approach. An unpackaged build cannot queue OS
        // toasts (AppNotification has no schedule API), so due instances are always shown
        // by the in-app loop in ExecuteAsync.
        var planned = await _planner.PlanAsync(_clock.UtcNow, TimeSpan.FromDays(30), cancellationToken);

        _logger.LogInformation("Reminder plan: {Planned} instance(s) planned.", planned);
    }

    private async Task<bool> ProcessDueAsync(CancellationToken cancellationToken, DuePass pass)
    {
        var now = _clock.UtcNow;
        var due = await _alerts.GetPendingInstancesAsync(now, cancellationToken);
        if (due.Count == 0)
        {
            return false;
        }

        var missed = new List<AlertInstance>();
        var focusAssistActive = _focusAssist.IsActive;
        var criticalBreaksThrough = _settings.Current.CriticalBreaksThroughDoNotDisturb;
        var deferred = false;

        foreach (var instance in due)
        {
            // On startup, alerts that came due long before the app ran are retired as
            // missed instead of being toasted (past alerts must not toast at startup).
            // A short grace window still fires alerts that came due just before launch.
            // Persistent chains survive this by queueing their next repeat from now.
            if (pass == DuePass.Startup && instance.FireAtUtc < now - FreshWindow)
            {
                await _alerts.MarkMissedAsync(instance.Id, now, cancellationToken);
                await _engine.TryScheduleRepeatAsync(instance, now, cancellationToken);
                continue;
            }

            // A repeat that was queued while the user was away must not nag a reminder
            // that has since been completed or deleted.
            if (!await _engine.ShouldFireAsync(instance, cancellationToken))
            {
                await _alerts.MarkDoneAsync(instance.Id, now, cancellationToken);
                continue;
            }

            // After sleep every pending instance is missed: retire it without a toast and
            // let one summary cover the whole wake-up. Persistent chains still queue their
            // next repeat from now, so an unfinished nagging reminder keeps nagging.
            if (pass == DuePass.Resume)
            {
                await _alerts.MarkMissedAsync(instance.Id, now, cancellationToken);
                await _engine.TryScheduleRepeatAsync(instance, now, cancellationToken);
                missed.Add(instance);
                continue;
            }

            var (title, timeLabel, meetingUrl, ownerPriority) =
                await GetOwnerSummaryAsync(instance, cancellationToken);
            var alertPriority = await GetAlertPriorityAsync(instance, cancellationToken);
            var priority = ownerPriority > alertPriority ? ownerPriority : alertPriority;

            // Focus Assist: hold non-critical notifications (and critical ones when the
            // user turned break-through off) as still-pending so they fire once Do Not
            // Disturb ends instead of being silently dropped.
            if (focusAssistActive && !(priority == Priority.Critical && criticalBreaksThrough))
            {
                deferred = true;
                continue;
            }

            if (!await _alerts.TryMarkFiredAsync(instance.Id, now, cancellationToken))
            {
                continue;
            }

            _logger.LogInformation(
                "Reminder fired: {InstanceId} at {FireAtUtc} (startup: {Startup}, priority: {Priority}).",
                instance.Id,
                instance.FireAtUtc,
                pass == DuePass.Startup,
                priority);

            await _engine.TryScheduleRepeatAsync(instance, now, cancellationToken);
            ShowToast(
                instance,
                pass == DuePass.Startup ? "Missed reminder" : "Reminder",
                title,
                timeLabel,
                meetingUrl,
                priority);
        }

        if (missed.Count > 0)
        {
            var count = MissedReminderSummary.Count(missed);
            _logger.LogInformation(
                "Resume catch-up retired {Instances} instance(s) as {Missed} missed reminder(s).",
                missed.Count,
                count);
            ShowMissedSummary(count);
        }

        return deferred;
    }

    private enum DuePass
    {
        Normal,
        Startup,
        Resume,
    }

    private void ShowToast(
        AlertInstance instance,
        string heading,
        string title,
        string timeLabel,
        string? meetingUrl,
        Priority priority)
    {
        try
        {
            var isHighPriority = priority >= Priority.High;

            // AppNotificationBuilder is the supported way to attach actions without hand
            // writing toast XML. The body and the Open button open the app; every button
            // carries action + instance id so ToastActivationService can resolve the instance.
            // The snooze menu is a combo box because Windows exposes selection inputs that way.
            var builder = new AppNotificationBuilder()
                .AddArgument("action", "open")
                .AddArgument("id", instance.Id)
                .SetTag(ToastCleanup.TagFor(instance.Id))
                .SetGroup(ToastCleanup.Group)
                .AddText(heading)
                .AddText(title)
                .AddText(timeLabel)
                .AddComboBox(CreateSnoozeMenu());

            builder.AddButton(CreateButton("Snooze", "snooze", instance.Id));
            builder.AddButton(CreateButton("Done", "done", instance.Id));
            builder.AddButton(CreateButton("Open", "open", instance.Id));

            if (!string.IsNullOrWhiteSpace(meetingUrl))
            {
                builder.AddButton(CreateButton("Join", "join", instance.Id));
            }

            // The reminder scenario keeps the toast on screen (and repeats its sound)
            // until the user acts on it, so a high-priority reminder cannot slip by.
            if (isHighPriority)
            {
                builder.SetScenario(AppNotificationScenario.Reminder);
            }

            // AppNotificationManager is the supported path without package identity. The
            // legacy ToastNotificationManager requires package identity or a Start menu
            // shortcut whose AppUserModelID matches the process.
            var notification = builder.BuildNotification();

            // Critical reminders ask Windows for a high-priority notification, which is
            // what lets them through Focus Assist when the user allowed break-through.
            if (priority == Priority.Critical)
            {
                notification.Priority = AppNotificationPriority.High;
            }

            AppNotificationManager.Default.Show(notification);

            _logger.LogInformation(
                "Reminder toast shown for {InstanceId} (high priority: {IsHighPriority}, priority: {Priority}).",
                instance.Id,
                isHighPriority,
                priority);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Could not display reminder notification for {InstanceId}.", instance.Id);
        }
    }

    /// <summary>
    /// One catch-up toast for the whole wake-up. Tag reuse means a later resume replaces
    /// the previous summary instead of stacking another one.
    /// </summary>
    private void ShowMissedSummary(int count)
    {
        try
        {
            var builder = new AppNotificationBuilder()
                .AddArgument("action", "open-overdue")
                .SetTag(ToastCleanup.SummaryTag)
                .SetGroup(ToastCleanup.Group)
                .AddText(MissedReminderSummary.Title(count))
                .AddText("Tap to review them in Overdue.");

            var open = new AppNotificationButton("Open Overdue");
            open.AddArgument("action", "open-overdue");
            builder.AddButton(open);

            AppNotificationManager.Default.Show(builder.BuildNotification());

            _logger.LogInformation("Missed reminder summary toast shown for {Count} reminder(s).", count);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Could not display the missed reminder summary.");
        }
    }

    private static AppNotificationButton CreateButton(string content, string action, string instanceId)
    {
        var button = new AppNotificationButton(content);
        button.AddArgument("action", action);
        button.AddArgument("id", instanceId);
        return button;
    }

    private AppNotificationComboBox CreateSnoozeMenu()
    {
        var comboBox = new AppNotificationComboBox(SnoozeSchedule.ToastInputId);
        comboBox.SetTitle("Snooze");

        foreach (var choice in SnoozeSchedule.Choices)
        {
            comboBox.AddItem(SnoozeSchedule.ToKey(choice), SnoozeSchedule.ToLabel(choice));
        }

        comboBox.SetSelectedItem(SnoozeSchedule.ToKey(DefaultSnoozeChoice()));
        return comboBox;
    }

    private SnoozeChoice DefaultSnoozeChoice()
    {
        var minutes = Math.Max(1, _settings.Current.DefaultSnoozeMinutes);
        return SnoozeSchedule.Choices.FirstOrDefault(
            choice => SnoozeSchedule.Minutes(choice) == minutes,
            SnoozeChoice.TenMinutes);
    }

    private async Task<Priority> GetAlertPriorityAsync(
        AlertInstance instance,
        CancellationToken cancellationToken)
    {
        try
        {
            var alert = await _alerts.GetAlertByIdAsync(instance.AlertId, cancellationToken);
            return alert?.Priority ?? Priority.Normal;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not load the priority for alert instance {InstanceId}.", instance.Id);
            return Priority.Normal;
        }
    }

    private async Task<(string Title, string TimeLabel, string? MeetingUrl, Priority Priority)> GetOwnerSummaryAsync(
        AlertInstance instance,
        CancellationToken cancellationToken)
    {
        string? title = null;
        string? meetingUrl = null;
        var priority = Priority.Normal;

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
                    var reminder = await _reminders.GetByIdAsync(instance.OwnerId, cancellationToken);
                    title = reminder?.Title;
                    priority = reminder?.Priority ?? Priority.Normal;
                    break;

                case OwnerType.Task:
                    var task = await _tasks.GetByIdAsync(instance.OwnerId, cancellationToken);
                    title = task?.Title;
                    priority = task?.Priority ?? Priority.Normal;
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

        return (string.IsNullOrWhiteSpace(title) ? "Reminder" : title, timeLabel, meetingUrl, priority);
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
