using E2E.Calendar.Core.Models;

namespace E2E.Calendar.Core.Abstractions;

public interface IRecurrenceExpander
{
    IEnumerable<Occurrence> Expand(
        CalendarEvent master,
        IEnumerable<CalendarEvent> exceptions,
        DateTimeOffset from,
        DateTimeOffset to);
}
