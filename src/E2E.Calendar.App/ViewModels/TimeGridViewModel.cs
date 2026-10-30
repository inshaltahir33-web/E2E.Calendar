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
    private readonly IReminderRepository _reminders;
    private readonly ITaskRepository _tasks;

    protected TimeGridViewModel(
        CalendarStateViewModel state,
        EventQueryService query,
        IEventQueryCache cache,
        SettingsService settings,
        WeatherService weather,
        IReminderRepository reminders,
        ITaskRepository tasks)
        : base(state, query, cache)
    {
        _settings = settings;
        _weather = weather;
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
            var items = (await Query.GetItemsAsync(period.FromUtc, period.ToUtc)).ToList();
            foreach (var reminder in await _reminders.GetInRangeAsync(period.FromUtc, period.ToUtc))
            {
                if (reminder.DoneUtc is null && !reminder.Deleted && reminder.DueUtc is { } due)
                {
                    items.Add(new CalendarItem
                    {
                        Id = reminder.Id,
                        Title = reminder.Title,
                        StartUtc = due,
                        EndUtc = due.AddMinutes(15),
                        Kind = CalendarItemKind.Reminder,
                        HasTime = true,
                        IsDone = false,
                    });
                }
            }

            foreach (var task in await _tasks.GetAllAsync())
            {
                if (task.DoneUtc is null && !task.Deleted && task.DueUtc is { } due &&
                    due >= period.FromUtc && due < period.ToUtc)
                {
                    items.Add(new CalendarItem
                    {
                        Id = task.Id,
                        Title = task.Title,
                        StartUtc = due,
                        EndUtc = task.HasTime ? due.AddMinutes(15) : due.AddDays(1),
                        Kind = CalendarItemKind.Task,
                        HasTime = task.HasTime,
                        IsDone = false,
                    });
                }
            }
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

            var visible = new HashSet<string>(
                days.Select(day => day.Date.ToString("yyyy-MM-dd")),
                StringComparer.Ordinal);

            var visibleItems = items
                .Where(item => visible.Contains(DateOnly.FromDateTime(timeZone.ToLocal(item.StartUtc)).ToString("yyyy-MM-dd")))
                .OrderBy(item => item.StartUtc)
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
        ClockFormat = settings.ClockFormat;
        ShowSecondaryTimeZone = settings.ShowSecondaryTimeZone;
        SecondaryTimeZoneId = settings.SecondaryTimeZoneId ?? State.LocalTimeZoneId;
        HourHeight = settings.HourHeight;
        WeekStart = settings.WeekStart;

        OnSettingsApplied(settings);
    }

    protected virtual void OnSettingsApplied(AppSettings settings)
    {
    }
}
