using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using E2E.Calendar.Core;
using E2E.Calendar.Core.Abstractions;
using E2E.Calendar.Core.Models;

namespace E2E.Calendar.App.ViewModels;

public sealed partial class CalendarStateViewModel : ObservableObject
{
    private readonly IClock _clock;

    [ObservableProperty]
    private DateTimeOffset _currentDate;

    [ObservableProperty]
    private CalendarViewMode _viewMode = CalendarViewMode.Week;

    public CalendarStateViewModel(IClock clock)
    {
        ArgumentNullException.ThrowIfNull(clock);

        _clock = clock;
        _currentDate = clock.UtcNow;
    }

    public string LocalTimeZoneId => _clock.LocalTimeZone.ToIanaId();

    public DateOnly LocalDate =>
        DateOnly.FromDateTime(_clock.LocalTimeZone.ToLocal(CurrentDate));

    public string PeriodLabel => CalendarPeriod.FormatLabel(ViewMode, LocalDate);

    public CalendarPeriod Period => CalendarPeriod.For(ViewMode, LocalDate, LocalTimeZoneId);

    [RelayCommand]
    public void GoToToday()
    {
        CurrentDate = _clock.UtcNow;
    }

    [RelayCommand]
    public void GoPrevious()
    {
        CurrentDate = ViewMode switch
        {
            CalendarViewMode.Month => CurrentDate.AddMonths(-1),
            CalendarViewMode.Day => CurrentDate.AddDays(-1),
            CalendarViewMode.Agenda => CurrentDate.AddDays(-1),
            _ => CurrentDate.AddDays(-7),
        };
    }

    [RelayCommand]
    public void GoNext()
    {
        CurrentDate = ViewMode switch
        {
            CalendarViewMode.Month => CurrentDate.AddMonths(1),
            CalendarViewMode.Day => CurrentDate.AddDays(1),
            CalendarViewMode.Agenda => CurrentDate.AddDays(1),
            _ => CurrentDate.AddDays(7),
        };
    }

    [RelayCommand]
    public void SetViewMode(CalendarViewMode mode)
    {
        ViewMode = mode;
    }

    partial void OnCurrentDateChanged(DateTimeOffset value)
    {
        OnPropertyChanged(nameof(LocalDate));
        OnPropertyChanged(nameof(PeriodLabel));
        OnPropertyChanged(nameof(Period));
    }

    partial void OnViewModeChanged(CalendarViewMode value)
    {
        OnPropertyChanged(nameof(PeriodLabel));
        OnPropertyChanged(nameof(Period));
    }
}
