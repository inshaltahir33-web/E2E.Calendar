using E2E.Calendar.Core.Abstractions;
using E2E.Calendar.Core.Services;

namespace E2E.Calendar.App.ViewModels;

public sealed class DayViewModel : TimeGridViewModel
{
    public DayViewModel(
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

    public override int DayCount => 1;
}
