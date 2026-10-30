using E2E.Calendar.Core;
using E2E.Calendar.Core.Abstractions;
using E2E.Calendar.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Windows.AppNotifications;

namespace E2E.Calendar.Platform;

/// <summary>
/// The single place that handles toast activation (toast tapped or Snooze / Done /
/// Open pressed), both at startup and while the app runs. Windows App SDK's
/// <see cref="AppNotificationManager"/> routes every toast of this app into
/// <see cref="AppNotificationManager.NotificationInvoked"/> after
/// <see cref="Register"/> has been called.
///
/// Toast payloads use flat key/value arguments: action=snooze|done|open, id=instance,
/// minutes=5|10|30|60|tomorrow.
/// </summary>
public sealed class ToastActivationService
{
    private readonly IAlertRepository _alerts;
    private readonly IClock _clock;
    private readonly ReminderNotificationService _scheduler;
    private readonly ILogger<ToastActivationService> _logger;

    public ToastActivationService(
        IAlertRepository alerts,
        IClock clock,
        ReminderNotificationService scheduler,
        ILogger<ToastActivationService> logger)
    {
        _alerts = alerts;
        _clock = clock;
        _scheduler = scheduler;
        _logger = logger;
    }

    /// <summary>Raised when the user taps the toast body or the Open button.</summary>
    public event EventHandler? OpenRequested;

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

            if (string.IsNullOrWhiteSpace(instanceId))
            {
                return;
            }

            switch (action)
            {
                case "snooze":
                    arguments.TryGetValue("minutes", out var minutes);
                    await SnoozeAsync(instanceId, minutes);
                    break;

                case "done":
                    await MarkDoneAsync(instanceId);
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

    /// <summary>Re-arms the alert after the chosen minutes and removes the toast.</summary>
    public async Task SnoozeAsync(string instanceId, string? minutes)
    {
        var instance = await _alerts.GetInstanceByIdAsync(instanceId);
        if (instance is null)
        {
            ToastCleanup.RemoveByTag(instanceId);
            return;
        }

        var now = _clock.UtcNow;
        var fireAt = IsTomorrow(minutes)
            ? TomorrowAtSameTime(now)
            : now.AddMinutes(ParseMinutes(minutes));

        // Retire the original instance and materialise the snoozed one. The copy keeps
        // SnoozedUntilUtc set so the planner never mistakes it for a plannable instance.
        await _alerts.MarkSnoozedAsync(instance.Id, fireAt);
        await _alerts.UpsertInstanceAsync(instance with
        {
            Id = SnoozeInstanceId(instance.Id, fireAt),
            FireAtUtc = fireAt,
            State = AlertState.Pending,
            SnoozedUntilUtc = fireAt,
            FiredUtc = null,
        });

        ToastCleanup.RemoveByTag(instance.Id);
        _scheduler.Wake();
    }

    /// <summary>Marks the alert done and removes its toast.</summary>
    public async Task MarkDoneAsync(string instanceId)
    {
        await _alerts.MarkDoneAsync(instanceId, _clock.UtcNow);
        ToastCleanup.RemoveByTag(instanceId);
        _scheduler.Wake();
    }

    public static string SnoozeInstanceId(string instanceId, DateTimeOffset fireAtUtc) =>
        $"{instanceId}:snooze:{fireAtUtc.UtcTicks}";

    private static bool IsTomorrow(string? minutes) =>
        string.Equals(minutes, "tomorrow", StringComparison.OrdinalIgnoreCase);

    private static int ParseMinutes(string? minutes) =>
        int.TryParse(minutes, out var value) && value > 0 ? value : 5;

    private DateTimeOffset TomorrowAtSameTime(DateTimeOffset nowUtc)
    {
        var timeZone = _clock.LocalTimeZone;
        var nowLocal = timeZone.ToLocal(nowUtc);
        var tomorrowLocal = nowLocal.Date.AddDays(1).Add(nowLocal.TimeOfDay);
        return timeZone.ToUtc(tomorrowLocal);
    }
}
