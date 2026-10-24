using CommunityToolkit.Mvvm.ComponentModel;
using E2E.Calendar.Core.Abstractions;
using E2E.Calendar.Core.Models;
using E2E.Calendar.Core.Services;

namespace E2E.Calendar.App.ViewModels;

public sealed partial class WeekViewModel : TimeGridViewModel
{
    [ObservableProperty]
    private bool _showWeekends = true;

    [ObservableProperty]
    private bool _showWeekNumbers;

    public WeekViewModel(
        CalendarStateViewModel state,
        EventQueryService query,
        IEventQueryCache cache,
        SettingsService settings,
        Services.WeatherService weather,
        IReminderRepository reminders,
        ITaskRepository tasks)
        : base(state, query, cache, settings, weather, reminders, tasks)
    {
    }

    public override int DayCount => ShowWeekends ? 7 : 5;

    protected override void OnSettingsApplied(AppSettings settings)
    {
        ShowWeekends = settings.ShowWeekends;
        ShowWeekNumbers = settings.ShowWeekNumbers;

        OnPropertyChanged(nameof(DayCount));
    }

    partial void OnShowWeekendsChanged(bool value)
    {
        OnPropertyChanged(nameof(DayCount));

        _ = RefreshAsync();
    }
}
