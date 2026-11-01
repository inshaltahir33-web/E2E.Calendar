using System.Diagnostics;
using E2E.Calendar.Core;
using E2E.Calendar.Core.Abstractions;
using E2E.Calendar.Core.Models;
using E2E.Calendar.Core.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Windows.AppNotifications;

namespace E2E.Calendar.Platform;

/// <summary>
/// The single place that handles toast activation (toast tapped or Snooze / Done /
/// Join / Open pressed), both at startup and while the app runs. Windows App SDK's
/// <see cref="AppNotificationManager"/> routes every toast of this app into
/// <see cref="AppNotificationManager.NotificationInvoked"/> after
/// <see cref="Register"/> has been called.
///
/// Toast payloads use flat key/value arguments: action=snooze|done|join|open|open-overdue,
/// id=instance (absent from the catch-up summary). Snooze reads the chosen option from the
/// toast's snooze menu (UserInput) and falls back to the default snooze from settings when
/// the menu is unavailable.
/// </summary>
public sealed class ToastActivationService
{
    private readonly IAlertRepository _alerts;
    private readonly IEventRepository _events;
    private readonly IClock _clock;
    private readonly ReminderNotificationService _scheduler;
    private readonly ReminderEngine _engine;
    private readonly ISettingsService _settings;
    private readonly ILogger<ToastActivationService> _logger;

    public ToastActivationService(
        IAlertRepository alerts,
        IEventRepository events,
        IClock clock,
        ReminderNotificationService scheduler,
        ReminderEngine engine,
        ISettingsService settings,
        ILogger<ToastActivationService> logger)
    {
        _alerts = alerts;
        _events = events;
        _clock = clock;
        _scheduler = scheduler;
        _engine = engine;
        _settings = settings;
        _logger = logger;
    }

    /// <summary>Raised when the user taps the toast body or the Open button.</summary>
    public event EventHandler? OpenRequested;

    /// <summary>Raised when the user opens the wake-from-sleep missed-reminder summary.</summary>
    public event EventHandler? OverdueRequested;

    /// <summary>
    /// Registers toast activation. Safe to call once at startup, before any toast is
    /// shown; failures are logged and never break app startup.
    /// </summary>
    public void Register()
    {
        try
        {
            AppNotificationManager.Default.NotificationInvoked += OnNotificationInvoked;
            AppNotificationManager.Default.Register();
            _logger.LogInformation("App notification activation registered.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Toast activation could not be registered; toast buttons will not respond.");
        }
    }

    private async void OnNotificationInvoked(AppNotificationManager sender, AppNotificationActivatedEventArgs args)
    {
        try
        {
            var arguments = args.Arguments;
            arguments.TryGetValue("action", out var action);
            arguments.TryGetValue("id", out var instanceId);

            _logger.LogInformation("Toast action: {Action} {InstanceId}", action ?? "open", instanceId);

            if (string.Equals(action, "open-overdue", StringComparison.Ordinal))
            {
                OverdueRequested?.Invoke(this, EventArgs.Empty);
                return;
            }

            if (string.IsNullOrWhiteSpace(instanceId))
            {
                return;
            }

            switch (action)
            {
                case "snooze":
                    string? choice = null;
                    if (args.UserInput is { } userInput &&
                        userInput.TryGetValue(SnoozeSchedule.ToastInputId, out var selected))
                    {
                        choice = selected;
                    }

                    await SnoozeAsync(instanceId, choice);
                    break;

                case "done":
                    await MarkDoneAsync(instanceId);
                    break;

                case "join":
                    await JoinAsync(instanceId);
                    break;

                default:
                    OpenRequested?.Invoke(this, EventArgs.Empty);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Toast activation handling failed.");
        }
    }

    /// <summary>Re-arms the alert at the chosen snooze option and removes the toast.</summary>
    public async Task SnoozeAsync(string instanceId, string? choice)
    {
        var instance = await _alerts.GetInstanceByIdAsync(instanceId);
        if (instance is null)
        {
            await ToastCleanup.RemoveByTagAsync(instanceId);
            return;
        }

        var now = _clock.UtcNow;
        var fireAt = SnoozeSchedule.TryParse(choice, out var snoozeChoice)
            ? SnoozeSchedule.Resolve(snoozeChoice, now, _clock.LocalTimeZone)
            : now.AddMinutes(Math.Max(1, _settings.Current.DefaultSnoozeMinutes));

        // Retire the original instance and materialise the snoozed one. The copy keeps
        // SnoozedUntilUtc set so the planner never mistakes it for a plannable instance.
        // Any repeat queued for the original chain is dropped first so snoozing cannot
        // leave two nagging chains running.
        await _engine.StopRepeatChainAsync(instance);
        await ToastCleanup.RemoveByTagAsync(QueuedRepeatTag(instance));
        await _alerts.MarkSnoozedAsync(instance.Id, fireAt);
        await _alerts.UpsertInstanceAsync(instance with
        {
            Id = SnoozeInstanceId(instance.Id, fireAt),
            FireAtUtc = fireAt,
            State = AlertState.Pending,
            SnoozedUntilUtc = fireAt,
            FiredUtc = null,
        });

        await ToastCleanup.RemoveByTagAsync(instance.Id);
        _scheduler.Wake();
    }

    /// <summary>Marks the alert done and removes its toast and any queued repeats.</summary>
    public async Task MarkDoneAsync(string instanceId)
    {
        var instance = await _alerts.GetInstanceByIdAsync(instanceId);

        await _alerts.MarkDoneAsync(instanceId, _clock.UtcNow);

        if (instance is not null)
        {
            await _engine.StopRepeatChainAsync(instance);
            await ToastCleanup.RemoveByTagAsync(QueuedRepeatTag(instance));
        }

        await ToastCleanup.RemoveByTagAsync(instanceId);
        _scheduler.Wake();
    }

    /// <summary>Opens the meeting link of the event behind the toast.</summary>
    private async Task JoinAsync(string instanceId)
    {
        var instance = await _alerts.GetInstanceByIdAsync(instanceId);
        if (instance is null || instance.OwnerType != OwnerType.Event)
        {
            return;
        }

        var calendarEvent = await _events.GetByIdAsync(instance.OwnerId);
        if (calendarEvent?.MeetingUrl is not { Length: > 0 } meetingUrl ||
            !Uri.TryCreate(meetingUrl, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            _logger.LogWarning("Toast join ignored: no usable meeting link for event {OwnerId}.", instance.OwnerId);
            return;
        }

        Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
    }

    private static string QueuedRepeatTag(AlertInstance instance) =>
        ReminderEngine.RepeatInstanceId(
            instance.AlertId,
            instance.OccurrenceUtc,
            instance.RepeatCount + 1);

    public static string SnoozeInstanceId(string instanceId, DateTimeOffset fireAtUtc) =>
        $"{instanceId}:snooze:{fireAtUtc.UtcTicks}";
}
