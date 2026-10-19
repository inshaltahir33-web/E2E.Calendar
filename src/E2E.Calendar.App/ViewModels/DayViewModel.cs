using E2E.Calendar.Core.Services;

namespace E2E.Calendar.App.ViewModels;

public sealed class DayViewModel : TimeGridViewModel
{
    public DayViewModel(CalendarStateViewModel state, EventQueryService query)
        : base(state, query)
    {
    }

    public override int DayCount => 1;
}
