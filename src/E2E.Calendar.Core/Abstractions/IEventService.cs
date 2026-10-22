using E2E.Calendar.Core.Models;

namespace E2E.Calendar.Core.Abstractions;

public interface IEventService
{
    Task<CalendarEvent> CreateAsync(EventDraft draft, CancellationToken cancellationToken = default);

    Task AddAlertsAsync(string eventId, IReadOnlyList<int> offsetMinutes, CancellationToken cancellationToken = default);

    Task<CalendarEvent> UpdateAsync(CalendarEvent calendarEvent, CancellationToken cancellationToken = default);

    Task<CalendarEvent> SoftDeleteAsync(string id, CancellationToken cancellationToken = default);

    Task<CalendarEvent> UndoDeleteAsync(string id, CancellationToken cancellationToken = default);

    Task<CalendarEvent> DuplicateAsync(string id, CancellationToken cancellationToken = default);
}
