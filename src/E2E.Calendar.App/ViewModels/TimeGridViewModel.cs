using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using E2E.Calendar.App.Controls;
using E2E.Calendar.Core;
using E2E.Calendar.Core.Abstractions;
using E2E.Calendar.Core.Models;
using E2E.Calendar.Core.Services;
using E2E.Calendar.App.Services;

namespace E2E.Calendar.App.ViewModels;

public abstract partial class TimeGridViewModel : CalendarPageViewModel
{
    private readonly SettingsService _settings;
    private readonly WeatherService _weather;
    private readonly CalendarItemQueryService _items;
    private readonly IReminderRepository _reminders;
    private readonly ITaskRepository _tasks;

    /// <summary>Reminders and tasks are hidden in Day/Week until this setting is turned on.</summary>
    [ObservableProperty]
    private bool _showRemindersAndTasks;

    protected TimeGridViewModel(
        CalendarStateViewModel state,
        EventQueryService query,
        IEventQueryCache cache,
        CalendarItemQueryService items,
        SettingsService settings,
        WeatherService weather,
        IReminderRepository reminders,
        ITaskRepository tasks)
        : base(state, query, cache)
    {
        _settings = settings;
        _weather = weather;
        _items = items;
        _reminders = reminders;
        _tasks = tasks;

        ApplySettings(_settings.Current);

        _settings.Changed += (_, updated) => ApplySettings(updated);
    }

    public IReadOnlyList<TimeGridDay> Days { get; private set; } = Array.Empty<TimeGridDay>();

    public IReadOnlyList<CalendarItem> Items { get; private set; } = Array.Empty<CalendarItem>();

    public abstract int DayCount { get; }

    public DateTimeOffset? Now => State.UtcNow;

    [ObservableProperty]
    private ClockFormat _clockFormat = ClockFormat.TwelveHour;

    [ObservableProperty]
    private bool _showSecondaryTimeZone;

    [ObservableProperty]
    private string? _secondaryTimeZoneId = "UTC";

    [ObservableProperty]
    private double _hourHeight = AppSettings.DefaultHourHeight;

    [ObservableProperty]
    private WeekStartDay _weekStart = WeekStartDay.Monday;

    [ObservableProperty]
    private string _weatherText = "Weather unavailable";

    public async Task RefreshWeatherAsync()
    {
        var snapshot = await _weather.GetLocalAsync();
        WeatherText = snapshot?.Display ?? "Weather unavailable";
    }

    public override async Task RefreshAsync()
    {
        await LoadPeriodAsync(async () =>
        {
            var period = State.Period;
            var items = await _items.GetItemsAsync(
                period.FromUtc,
                period.ToUtc,
                ShowRemindersAndTasks);
            var timeZone = TimeZoneInfo.Local;
            var today = State.LocalDate;

            var days = new List<TimeGridDay>(DayCount);
            var start = DateOnly.FromDateTime(timeZone.ToLocal(period.FromUtc));

            for (var index = 0; index < DayCount; index++)
            {
                var date = start.AddDays(index);

                days.Add(new TimeGridDay(
                    date,
                    date.ToString("ddd d MMM", System.Globalization.CultureInfo.CurrentCulture),
                    date.ToString("ddd", System.Globalization.CultureInfo.CurrentCulture),
                    date == today,
                    date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday));
            }

            // Keep everything that overlaps the rendered days (a banner may have
            // started before the range) so multi-day items never disappear.
            var rangeStartUtc = timeZone.ToUtc(days[0].Date.ToDateTime(TimeOnly.MinValue));
            var rangeEndUtc = timeZone.ToUtc(days[^1].Date.AddDays(1).ToDateTime(TimeOnly.MinValue));

            var visibleItems = items
                .Where(item => item.StartUtc < rangeEndUtc && item.EndUtc > rangeStartUtc)
                .OrderBy(item => item.StartUtc)
                .ThenBy(item => item.Id, StringComparer.Ordinal)
                .ToList();

            Days = days;
            Items = visibleItems;

            OnPropertyChanged(nameof(Days));
            OnPropertyChanged(nameof(Items));
        });
    }

    public async Task ToggleCompletedAsync(CalendarItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        var now = DateTimeOffset.UtcNow;

        if (item.Kind == CalendarItemKind.Reminder)
        {
            var reminder = await _reminders.GetByIdAsync(item.Id);

            if (reminder is not null)
            {
                await _reminders.UpsertAsync(
                    reminder with { DoneUtc = item.IsDone ? now : null, UpdatedUtc = now });
            }
        }
        else if (item.Kind == CalendarItemKind.Task)
        {
            var task = await _tasks.GetByIdAsync(item.Id);

            if (task is not null)
            {
                await _tasks.UpsertAsync(task with { DoneUtc = item.IsDone ? now : null });
            }
        }
    }

    private void ApplySettings(AppSettings settings)
    {
        var showRemindersAndTasks = settings.ShowRemindersAndTasks;
        var visibilityChanged = ShowRemindersAndTasks != showRemindersAndTasks;
        ShowRemindersAndTasks = showRemindersAndTasks;

        ClockFormat = settings.ClockFormat;
        ShowSecondaryTimeZone = settings.ShowSecondaryTimeZone;
        SecondaryTimeZoneId = settings.SecondaryTimeZoneId ?? State.LocalTimeZoneId;
        HourHeight = settings.HourHeight;
        WeekStart = settings.WeekStart;

        OnSettingsApplied(settings);

        if (visibilityChanged)
        {
            _ = RefreshAsync();
        }
    }

    protected virtual void OnSettingsApplied(AppSettings settings)
    {
    }
}
