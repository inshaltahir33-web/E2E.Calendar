using E2E.Calendar.Core.Models;

namespace E2E.Calendar.Core.Abstractions;

public interface ICalendarSubscriptionRepository
{
    Task<IReadOnlyList<CalendarSubscription>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<CalendarSubscription?> GetAsync(string calendarId, CancellationToken cancellationToken = default);

    Task UpsertAsync(CalendarSubscription subscription, CancellationToken cancellationToken = default);

    Task DeleteAsync(string calendarId, CancellationToken cancellationToken = default);
}
