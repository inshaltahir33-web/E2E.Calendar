using E2E.Calendar.Core.Models;

namespace E2E.Calendar.App.Controls;

public sealed record TimeGridDay(
    DateOnly Date,
    string Header,
    string WeekdayLabel,
    bool IsToday,
    bool IsWeekend)
{
    public TimeGridDay(DateOnly date, string header)
        : this(date, header, date.ToString("ddd", System.Globalization.CultureInfo.CurrentCulture), false, false)
    {
    }
}

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

public enum TimeGridItemAction
{
    Edit,
    Duplicate,
    Delete,
    ChangeColor,
}

public sealed class TimeGridItemActionEventArgs : EventArgs
{
    public TimeGridItemActionEventArgs(CalendarItem item, TimeGridItemAction action)
    {
        Item = item;
        Action = action;
    }

    public CalendarItem Item { get; }

    public TimeGridItemAction Action { get; }
}
