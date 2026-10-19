using CommunityToolkit.Mvvm.ComponentModel;
using E2E.Calendar.Core.Services;

namespace E2E.Calendar.App.ViewModels;

public sealed partial class WeekViewModel : TimeGridViewModel
{
    [ObservableProperty]
    private bool _showWeekends = true;

    public WeekViewModel(CalendarStateViewModel state, EventQueryService query)
        : base(state, query)
    {
    }

    public override int DayCount => ShowWeekends ? 7 : 5;

    partial void OnShowWeekendsChanged(bool value)
    {
        _ = RefreshAsync();
    }
}
