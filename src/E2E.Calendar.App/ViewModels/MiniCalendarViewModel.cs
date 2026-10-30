using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using E2E.Calendar.Core;
using E2E.Calendar.Core.Abstractions;
using E2E.Calendar.Core.Models;
using E2E.Calendar.Core.Services;
using Microsoft.UI.Xaml;

namespace E2E.Calendar.App.ViewModels;

public sealed class MiniCalendarDayViewModel : ObservableObject
{
    public required DateOnly Date { get; init; }

    public required string Label { get; init; }

    public required bool IsCurrentMonth { get; init; }

    public required bool IsToday { get; init; }

    public required bool IsSelected { get; init; }

    public required bool IsInSelectedWeek { get; init; }

    public required int EventCount { get; init; }

    public required string AutomationName { get; init; }

    public double MonthOpacity => IsCurrentMonth ? 1d : 0.4d;

    public Visibility WeekVisibility => IsInSelectedWeek ? Visibility.Visible : Visibility.Collapsed;

    public Visibility TodayVisibility => IsToday ? Visibility.Visible : Visibility.Collapsed;

    public Visibility NotTodayVisibility => IsToday ? Visibility.Collapsed : Visibility.Visible;

    public Visibility HasEventsVisibility => EventCount > 0 ? Visibility.Visible : Visibility.Collapsed;
}

public sealed partial class MiniCalendarViewModel : ObservableObject
{
    private readonly CalendarStateViewModel _state;
    private readonly IShellState _shellState;
    private readonly EventQueryService _query;
    private readonly IClock _clock;

    [ObservableProperty]
    private int _visibleYear;

    [ObservableProperty]
    private int _visibleMonth;

    [ObservableProperty]
    private string _headerText = string.Empty;

    [ObservableProperty]
    private IReadOnlyList<string> _weekdayHeaders = Array.Empty<string>();

    [ObservableProperty]
    private IReadOnlyList<MiniCalendarDayViewModel> _days = Array.Empty<MiniCalendarDayViewModel>();

    public MiniCalendarViewModel(
        CalendarStateViewModel state,
        IShellState shellState,
        EventQueryService query,
        IClock clock)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(shellState);
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(clock);

        _state = state;
        _shellState = shellState;
        _query = query;
        _clock = clock;

        _state.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(CalendarStateViewModel.WeekStart))
            {
                _ = RefreshAsync();
            }
        };

        var today = DateOnly.FromDateTime(clock.LocalTimeZone.ToLocal(clock.UtcNow));
        VisibleYear = today.Year;
        VisibleMonth = today.Month;
    }

    public async Task RefreshAsync()
    {
        var today = DateOnly.FromDateTime(_clock.LocalTimeZone.ToLocal(_clock.UtcNow));
        var timeZone = _clock.LocalTimeZone;

        var firstOfMonth = new DateOnly(VisibleYear, VisibleMonth, 1);
        var lastOfMonth = firstOfMonth.AddDays(DateTime.DaysInMonth(VisibleYear, VisibleMonth) - 1);

        var fromUtc = timeZone.ToUtc(firstOfMonth.ToDateTime(TimeOnly.MinValue));
        var toUtc = timeZone.ToUtc(lastOfMonth.AddDays(1).ToDateTime(TimeOnly.MinValue));

        var items = await _query.GetItemsAsync(fromUtc, toUtc);

        var counts = items
            .GroupBy(item => DateOnly.FromDateTime(timeZone.ToLocal(item.StartUtc)))
            .ToDictionary(group => group.Key, group => group.Count());

        var grid = MiniCalendarGridBuilder.Build(
            VisibleYear,
            VisibleMonth,
            _state.WeekStart,
            today,
            _state.SelectedDate,
            counts);

        var culture = CultureInfo.CurrentCulture;

        HeaderText = firstOfMonth.ToString("MMMM yyyy", culture);

        WeekdayHeaders = BuildWeekdayHeaders(culture, _state.WeekStart);

        var days = new List<MiniCalendarDayViewModel>();

        foreach (var week in grid.Weeks)
        {
            var weekDays = new List<MiniCalendarDayViewModel>(week.Days.Count);

            foreach (var day in week.Days)
            {
                var dateText = day.Date.ToString("D", culture);

                weekDays.Add(new MiniCalendarDayViewModel
                {
                    Date = day.Date,
                    Label = day.Date.Day.ToString(culture),
                    IsCurrentMonth = day.IsCurrentMonth,
                    IsToday = day.IsToday,
                    IsSelected = day.IsSelected,
                    IsInSelectedWeek = day.IsInSelectedWeek,
                    EventCount = day.EventCount,
                    AutomationName = day.EventCount switch
                    {
                        0 => dateText,
                        1 => string.Format(culture, "{0}, 1 event", dateText),
                        _ => string.Format(culture, "{0}, {1} events", dateText, day.EventCount),
                    },
                });
            }

            days.AddRange(weekDays);
        }

        Days = days;
    }

    [RelayCommand]
    public void PreviousMonth()
    {
        MoveMonth(-1);
    }

    [RelayCommand]
    public void NextMonth()
    {
        MoveMonth(1);
    }

    [RelayCommand]
    public void SelectDay(MiniCalendarDayViewModel? day)
    {
        if (day is not null)
        {
            _state.SelectDateCommand.Execute(day.Date);
            _shellState.SetSelectedDate(day.Date);
        }
    }

    [RelayCommand]
    public void MoveSelection(int days)
    {
        _state.SelectDateCommand.Execute(_state.SelectedDate.AddDays(days));
    }

    [RelayCommand]
    public void MoveToFirstOfMonth()
    {
        _state.SelectDateCommand.Execute(new DateOnly(VisibleYear, VisibleMonth, 1));
    }

    [RelayCommand]
    public void Scroll(int delta)
    {
        MoveMonth(delta);
    }

    public void MoveMonth(int delta)
    {
        var date = new DateOnly(VisibleYear, VisibleMonth, 1).AddMonths(delta);

        VisibleYear = date.Year;
        VisibleMonth = date.Month;
    }

    private static IReadOnlyList<string> BuildWeekdayHeaders(CultureInfo culture, WeekStartDay weekStart)
    {
        var names = culture.DateTimeFormat.ShortestDayNames;
        var headers = new List<string>(MiniCalendarGridBuilder.DaysPerWeek);

        for (var index = 0; index < MiniCalendarGridBuilder.DaysPerWeek; index++)
        {
            var day = (DayOfWeek)((index + (int)weekStart) % MiniCalendarGridBuilder.DaysPerWeek);
            var name = names[(int)day];

            headers.Add(name.Length > 2 ? name[..2] : name);
        }

        return headers;
    }
}
