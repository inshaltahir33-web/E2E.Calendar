using System.Collections.ObjectModel;
using System.Globalization;
using System.Runtime.InteropServices;
using E2E.Calendar.App.ViewModels;
using E2E.Calendar.Core;
using E2E.Calendar.Core.Abstractions;
using E2E.Calendar.Core.Models;
using E2E.Calendar.Core.Services;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Windows.Graphics;

namespace E2E.Calendar.App.Views;

/// <summary>
/// Borderless always-on-top flyout shown from the tray icon: the next upcoming
/// event plus today's agenda. It is hidden (not closed) when it loses focus so
/// the tray icon can reopen it cheaply.
/// </summary>
public sealed partial class TrayFlyoutWindow : Window
{
    private const int FlyoutWidth = 360;
    private const int FlyoutHeight = 440;
    private const int ScreenMargin = 12;

    private readonly AgendaService _agenda;
    private readonly EventQueryService _query;
    private readonly IClock _clock;
    private readonly ILogger<TrayFlyoutWindow> _logger;
    private readonly ObservableCollection<TrayAgendaItem> _today = new();

    public TrayFlyoutWindow(
        AgendaService agenda,
        EventQueryService query,
        IClock clock,
        ILogger<TrayFlyoutWindow> logger)
    {
        ArgumentNullException.ThrowIfNull(agenda);
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(logger);

        _agenda = agenda;
        _query = query;
        _clock = clock;
        _logger = logger;

        InitializeComponent();

        TodayList.ItemsSource = _today;

        ConfigurePresenter();
        Activated += OnActivated;
        Closed += OnClosed;
    }

    public bool IsOpen { get; private set; }

    public void ShowFlyout() => _ = RefreshAndShowAsync();

    public void HideFlyout()
    {
        if (!IsOpen)
        {
            return;
        }

        AppWindow.Hide();
        IsOpen = false;
    }

    private void ConfigurePresenter()
    {
        AppWindow.IsShownInSwitchers = false;

        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.SetBorderAndTitleBar(false, false);
            presenter.IsAlwaysOnTop = true;
            presenter.IsResizable = false;
        }
    }

    private async Task RefreshAndShowAsync()
    {
        try
        {
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "The tray flyout could not load the agenda.");
            EmptyToday.Visibility = Visibility.Visible;
        }

        PositionWindow();

        if (!IsOpen)
        {
            Activate();
            IsOpen = true;
        }
    }

    private async Task RefreshAsync()
    {
        var now = _clock.UtcNow;
        var timeZone = _clock.LocalTimeZone;
        var culture = CultureInfo.CurrentCulture;
        var today = DateOnly.FromDateTime(timeZone.ToLocal(now));
        var todayStartUtc = timeZone.ToUtc(today.ToDateTime(TimeOnly.MinValue));
        var tomorrowStartUtc = timeZone.ToUtc(today.AddDays(1).ToDateTime(TimeOnly.MinValue));

        DateText.Text = today.ToDateTime(TimeOnly.MinValue).ToString("ddd d MMM", culture);

        var upcoming = await _query.GetItemsAsync(todayStartUtc, now.AddDays(7));
        var next = upcoming
            .Where(item => item.EndUtc >= now)
            .OrderBy(item => item.StartUtc)
            .FirstOrDefault();

        if (next is null)
        {
            NextEventTitle.Text = "No upcoming events";
            NextEventDetail.Text = string.Empty;
        }
        else
        {
            NextEventTitle.Text = next.Title;
            NextEventDetail.Text = string.IsNullOrWhiteSpace(next.Location)
                ? FormatNextEventWhen(next, timeZone, culture)
                : $"{FormatNextEventWhen(next, timeZone, culture)} \u00B7 {next.Location}";
        }

        var days = await _agenda.GetDaysAsync(todayStartUtc, tomorrowStartUtc);
        var items = days.FirstOrDefault(day => day.Date == today)?.Items ?? Array.Empty<CalendarItem>();

        _today.Clear();

        foreach (var item in items)
        {
            _today.Add(new TrayAgendaItem(FormatTime(item, timeZone), item.Title, item.IsDone));
        }

        EmptyToday.Visibility = _today.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private static string FormatNextEventWhen(CalendarItem item, TimeZoneInfo timeZone, CultureInfo culture) =>
        item.AllDay
            ? "All day"
            : timeZone.ToLocal(item.StartUtc).ToString("ddd d MMM \u00B7 t", culture);

    private static string FormatTime(CalendarItem item, TimeZoneInfo timeZone)
    {
        if (item.AllDay)
        {
            return "All day";
        }

        return item.HasTime ? timeZone.ToLocal(item.StartUtc).ToString("t") : string.Empty;
    }

    private void PositionWindow()
    {
        var point = GetCursorPosition();
        var display = DisplayArea.GetFromPoint(
            new PointInt32(point.X, point.Y),
            DisplayAreaFallback.Primary);
        var work = display.WorkArea;

        AppWindow.MoveAndResize(new RectInt32(
            work.X + work.Width - FlyoutWidth - ScreenMargin,
            work.Y + work.Height - FlyoutHeight - ScreenMargin,
            FlyoutWidth,
            FlyoutHeight));
    }

    private void OnActivated(object sender, WindowActivatedEventArgs args)
    {
        if (args.WindowActivationState == WindowActivationState.Deactivated)
        {
            HideFlyout();
        }
    }

    private void OnClosed(object sender, WindowEventArgs args)
    {
        Activated -= OnActivated;
        Closed -= OnClosed;
        IsOpen = false;
    }

    private void OnNewEventClick(object sender, RoutedEventArgs e)
    {
        HideFlyout();
        App.Current.ShowNewEvent();
    }

    private void OnNewReminderClick(object sender, RoutedEventArgs e)
    {
        HideFlyout();
        App.Current.ShowQuickAdd();
    }

    private void OnOpenClick(object sender, RoutedEventArgs e)
    {
        HideFlyout();
        App.Current.ShowMainWindow();
    }

    private static (int X, int Y) GetCursorPosition() =>
        GetCursorPos(out var point) ? (point.X, point.Y) : (0, 0);

    [StructLayout(LayoutKind.Sequential)]
    private struct CursorPoint
    {
        public int X;
        public int Y;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out CursorPoint point);
}
