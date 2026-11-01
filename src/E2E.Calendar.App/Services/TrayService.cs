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
    private const uint Remind10Command = 3;
    private const uint Remind30Command = 4;
    private const uint Remind60Command = 5;
    private const uint OpenCommand = 6;
    private const uint QuitCommand = 7;

    private static readonly IReadOnlyList<TrayMenuEntry> Menu =
    [
        new(NewEventCommand, "New event"),
        new(NewReminderCommand, "New reminder"),
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
    private readonly ILogger<TrayService> _logger;
    private TrayFlyoutWindow? _flyout;

    public TrayService(
        IServiceProvider services,
        TrayIconService icon,
        ReminderEngine engine,
        ReminderNotificationService scheduler,
        IClock clock,
        ILogger<TrayService> logger)
    {
        _services = services;
        _icon = icon;
        _engine = engine;
        _scheduler = scheduler;
        _clock = clock;
        _logger = logger;

        _icon.Activated += OnIconActivated;
        _icon.ContextMenuRequested += OnContextMenuRequested;
        _icon.MenuCommandInvoked += OnMenuCommandInvoked;
    }

    public bool IsActive => _icon.IsVisible;

    public bool Start() => _icon.Start();

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
