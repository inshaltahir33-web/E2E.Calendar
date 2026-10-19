using E2E.Calendar.Core.Models;

namespace E2E.Calendar.Core.Abstractions;

public interface IEventRepository
{
    Task<CalendarEvent?> GetByIdAsync(string id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CalendarEvent>> GetInRangeAsync(
        string? calendarId,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CalendarEvent>> GetSoftDeletedAsync(
        DateTimeOffset deletedBeforeUtc,
        CancellationToken cancellationToken = default);

    Task UpsertAsync(CalendarEvent calendarEvent, CancellationToken cancellationToken = default);
}
