using E2E.Calendar.App.Views;
using E2E.Calendar.Core;
using E2E.Calendar.Core.Abstractions;
using E2E.Calendar.Core.Models;
using E2E.Calendar.Core.Services;
using E2E.Calendar.Platform;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace E2E.Calendar.App.Services;

/// <summary>
/// Connects the native tray icon to the app: left click opens the mini agenda
/// flyout, right click shows the quick-action menu, and Quit is the only path
/// that actually terminates the process.
/// </summary>
public sealed class TrayService : IDisposable
{
    private const uint NewEventCommand = 1;
    private const uint NewReminderCommand = 2;
    private const uint TodayAgendaCommand = 3;
    private const uint Remind10Command = 4;
    private const uint Remind30Command = 5;
    private const uint Remind60Command = 6;
    private const uint OpenCommand = 7;
    private const uint QuitCommand = 8;

    public static readonly IReadOnlyList<TrayMenuEntry> Menu =
    [
        new(NewEventCommand, "New event"),
        new(NewReminderCommand, "New reminder"),
        new(TodayAgendaCommand, "Today's agenda"),
        new(0, string.Empty, IsSeparator: true),
        new(Remind10Command, "Remind me in 10 min"),
        new(Remind30Command, "Remind me in 30 min"),
        new(Remind60Command, "Remind me in 60 min"),
        new(0, string.Empty, IsSeparator: true),
        new(OpenCommand, "Open"),
        new(QuitCommand, "Quit"),
    ];

    private readonly IServiceProvider _services;
    private readonly TrayIconService _icon;
    private readonly ReminderEngine _engine;
    private readonly ReminderNotificationService _scheduler;
    private readonly IClock _clock;
    private readonly IEventRepository _events;
    private readonly IReminderRepository _reminders;
    private readonly ILogger<TrayService> _logger;
    private TrayFlyoutWindow? _flyout;

    public TrayService(
        IServiceProvider services,
        TrayIconService icon,
        ReminderEngine engine,
        ReminderNotificationService scheduler,
        IClock clock,
        IEventRepository events,
        IReminderRepository reminders,
        ILogger<TrayService> logger)
    {
        _services = services;
        _icon = icon;
        _engine = engine;
        _scheduler = scheduler;
        _clock = clock;
        _events = events;
        _reminders = reminders;
        _logger = logger;

        _icon.Activated += OnIconActivated;
        _icon.ContextMenuRequested += OnContextMenuRequested;
        _icon.MenuCommandInvoked += OnMenuCommandInvoked;
    }

    public bool IsActive => _icon.IsVisible;

    public bool Start()
    {
        var started = _icon.Start();

        if (started)
        {
            _ = RefreshTooltipAsync();
        }

        return started;
    }

    private void OnIconActivated(object? sender, EventArgs e) => ToggleFlyout();

    private void OnContextMenuRequested(object? sender, EventArgs e) => _icon.ShowContextMenu(Menu);

    private void ToggleFlyout()
    {
        _flyout ??= _services.GetRequiredService<TrayFlyoutWindow>();

        if (_flyout.IsOpen)
        {
            _flyout.HideFlyout();
        }
        else
        {
            _flyout.ShowFlyout();
        }
    }

    private async void OnMenuCommandInvoked(object? sender, uint command)
    {
        try
        {
            switch (command)
            {
                case NewEventCommand:
                    App.Current.ShowNewEvent();
                    break;

                case NewReminderCommand:
                    App.Current.ShowQuickAdd(reminderMode: true);
                    break;

                case TodayAgendaCommand:
                    ToggleFlyout();
                    break;

                case Remind10Command:
                    await CreateReminderAsync(10);
                    break;

                case Remind30Command:
                    await CreateReminderAsync(30);
                    break;

                case Remind60Command:
                    await CreateReminderAsync(60);
                    break;

                case OpenCommand:
                    App.Current.ShowMainWindow();
                    break;

                case QuitCommand:
                    await App.Current.QuitAsync();
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Tray command {Command} failed.", command);
        }
    }

    private async Task CreateReminderAsync(int minutes)
    {
        await _engine.CreateCountdownAsync(
            minutes,
            _clock.UtcNow,
            _clock.LocalTimeZone.ToIanaId());

        _scheduler.Wake();
    }

    public async Task RefreshTooltipAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var now = _clock.UtcNow;
            var horizon = now.AddHours(24);

            var events = await _events.GetInRangeAsync(null, now, horizon, cancellationToken);
            var reminders = await _reminders.GetInRangeAsync(now, horizon, cancellationToken);

            DateTimeOffset? nextTime = null;
            string? nextTitle = null;

            foreach (var item in events)
            {
                if (!item.Deleted && (nextTime is null || item.StartUtc < nextTime))
                {
                    nextTime = item.StartUtc;
                    nextTitle = item.Title;
                }
            }

            foreach (var reminder in reminders)
            {
                if (reminder.DoneUtc is null && (nextTime is null || reminder.DueUtc < nextTime))
                {
                    nextTime = reminder.DueUtc;
                    nextTitle = reminder.Title;
                }
            }

            if (nextTime is null || string.IsNullOrWhiteSpace(nextTitle))
            {
                _icon.SetTooltip("E2E Calendar");
                return;
            }

            var local = TimeZoneInfo.ConvertTime(nextTime.Value, _clock.LocalTimeZone);
            _icon.SetTooltip($"Next: {nextTitle} {local:HH:mm}");
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "The tray tooltip could not be refreshed.");
            _icon.SetTooltip("E2E Calendar");
        }
    }

    public void Dispose()
    {
        _icon.Activated -= OnIconActivated;
        _icon.ContextMenuRequested -= OnContextMenuRequested;
        _icon.MenuCommandInvoked -= OnMenuCommandInvoked;

        _flyout?.Close();
        _flyout = null;

        _icon.Dispose();
    }
}
