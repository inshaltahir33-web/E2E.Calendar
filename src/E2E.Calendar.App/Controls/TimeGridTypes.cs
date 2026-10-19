using E2E.Calendar.Core.Models;

namespace E2E.Calendar.App.Controls;

public sealed record TimeGridDay(DateOnly Date, string Header);

public sealed class TimeGridItemEventArgs : EventArgs
{
    public TimeGridItemEventArgs(CalendarItem item)
    {
        Item = item;
    }

    public CalendarItem Item { get; }
}

public sealed class TimeGridSlotEventArgs : EventArgs
{
    public TimeGridSlotEventArgs(DateTimeOffset startUtc, DateTimeOffset endUtc)
    {
        StartUtc = startUtc;
        EndUtc = endUtc;
    }

    public DateTimeOffset StartUtc { get; }

    public DateTimeOffset EndUtc { get; }
}
