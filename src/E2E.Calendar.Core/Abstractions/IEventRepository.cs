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

    Task<IReadOnlyList<CalendarEvent>> SearchAsync(
        EventSearchCriteria criteria,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CalendarEvent>> GetSoftDeletedAsync(
        DateTimeOffset deletedBeforeUtc,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CalendarEvent>> GetExceptionsAsync(
        string masterId,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken = default);

    Task UpsertAsync(CalendarEvent calendarEvent, CancellationToken cancellationToken = default);

    Task DeleteByCalendarIdsAsync(IReadOnlyList<string> calendarIds, CancellationToken cancellationToken = default);
}
