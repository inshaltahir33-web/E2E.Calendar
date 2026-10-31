using E2E.Calendar.Core.Models;
using Microsoft.UI.Xaml;

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
    public TimeGridItemEventArgs(CalendarItem item, FrameworkElement? anchor = null)
    {
        Item = item;
        Anchor = anchor;
    }

    public CalendarItem Item { get; }

    /// <summary>The chip or bar that was clicked, used to place the details card.</summary>
    public FrameworkElement? Anchor { get; }
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

public enum TimeGridSlotAction
{
    NewEvent,
    NewReminder,
}

public sealed class TimeGridSlotActionEventArgs : EventArgs
{
    public TimeGridSlotActionEventArgs(DateOnly date, DateTimeOffset startUtc, TimeGridSlotAction action)
    {
        Date = date;
        StartUtc = startUtc;
        Action = action;
    }

    public DateOnly Date { get; }

    public DateTimeOffset StartUtc { get; }

    public TimeGridSlotAction Action { get; }
}
