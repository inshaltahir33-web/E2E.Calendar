using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using E2E.Calendar.Core;
using E2E.Calendar.Core.Abstractions;
using E2E.Calendar.Core.Models;
using E2E.Calendar.Core.Services;

namespace E2E.Calendar.App.ViewModels;

public sealed partial class CalendarStateViewModel : ObservableObject
{
    private readonly IClock _clock;
    private readonly ISettingsService _settings;

    [ObservableProperty]
    private DateTimeOffset _currentDate;

    [ObservableProperty]
    private DateOnly _selectedDate;

    [ObservableProperty]
    private WeekStartDay _weekStart = WeekStartDay.Monday;

    [ObservableProperty]
    private CalendarViewMode _viewMode = CalendarViewMode.Week;

    public CalendarStateViewModel(IClock clock, ISettingsService settings)
    {
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(settings);

        _clock = clock;
        _settings = settings;
        _weekStart = settings.WeekStart;
        _currentDate = clock.UtcNow;
        _selectedDate = DateOnly.FromDateTime(clock.LocalTimeZone.ToLocal(clock.UtcNow));

        settings.PropertyChanged += OnSettingsPropertyChanged;
    }

    private void OnSettingsPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ISettingsService.WeekStart))
        {
            WeekStart = _settings.WeekStart;
        }
    }

    public string LocalTimeZoneId => _clock.LocalTimeZone.ToIanaId();

    public DateTimeOffset UtcNow => _clock.UtcNow;

    public TimeZoneInfo LocalTimeZone => _clock.LocalTimeZone;

    public DateOnly LocalDate =>
        DateOnly.FromDateTime(_clock.LocalTimeZone.ToLocal(CurrentDate));

    public string PeriodLabel => CalendarPeriod.FormatLabel(ViewMode, LocalDate, weekStart: WeekStart);

    public CalendarPeriod Period => CalendarPeriod.For(ViewMode, LocalDate, LocalTimeZoneId, WeekStart);

    [RelayCommand]
    public void GoToToday()
    {
        CurrentDate = _clock.UtcNow;
        SelectDate(DateOnly.FromDateTime(_clock.LocalTimeZone.ToLocal(CurrentDate)));
    }

    /// <summary>Moves the visible period one unit back: Day 1 day, Week 7 days, Month 1 month, Agenda its range.</summary>
    [RelayCommand]
    public void GoPrevious() => MovePeriod(-1);

    /// <summary>Moves the visible period one unit forward: Day 1 day, Week 7 days, Month 1 month, Agenda its range.</summary>
    [RelayCommand]
    public void GoNext() => MovePeriod(1);

    /// <summary>
    /// Shifts the visible period by whole LOCAL calendar units (Day 1 day, Week 7 days,
    /// Month 1 month, Agenda its range) and re-anchors at local midnight, so month lengths
    /// and the local UTC offset can never move the period by an extra day.
    /// </summary>
    private void MovePeriod(int direction)
    {
        var anchor = LocalDate;
        var target = ViewMode switch
        {
            CalendarViewMode.Day => anchor.AddDays(direction),
            CalendarViewMode.Week => anchor.AddDays(7 * direction),
            CalendarViewMode.Month => anchor.AddMonths(direction),
            CalendarViewMode.Agenda => anchor.AddDays(CalendarPeriod.AgendaDays * direction),
            _ => anchor.AddDays(direction),
        };

        CurrentDate = _clock.LocalTimeZone.ToUtc(target.ToDateTime(TimeOnly.MinValue));
    }

    [RelayCommand]
    public void SetViewMode(CalendarViewMode mode)
    {
        ViewMode = mode;
    }

    [RelayCommand]
    public void SelectDate(DateOnly date)
    {
        SelectedDate = date;
        CurrentDate = _clock.LocalTimeZone.ToUtc(date.ToDateTime(TimeOnly.MinValue));
    }

    partial void OnCurrentDateChanged(DateTimeOffset value)
    {
        OnPropertyChanged(nameof(LocalDate));
        OnPropertyChanged(nameof(PeriodLabel));
        OnPropertyChanged(nameof(Period));
    }

    partial void OnSelectedDateChanged(DateOnly value) => OnPropertyChanged(nameof(SelectedDate));

    partial void OnViewModeChanged(CalendarViewMode value)
    {
        OnPropertyChanged(nameof(PeriodLabel));
        OnPropertyChanged(nameof(Period));
    }

    partial void OnWeekStartChanged(WeekStartDay value)
    {
        OnPropertyChanged(nameof(WeekStart));
        OnPropertyChanged(nameof(PeriodLabel));
        OnPropertyChanged(nameof(Period));
    }
}
