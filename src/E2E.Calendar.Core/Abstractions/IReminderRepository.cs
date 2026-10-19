using E2E.Calendar.Core.Models;

namespace E2E.Calendar.Core.Abstractions;

public interface IReminderRepository
{
    Task<Reminder?> GetByIdAsync(string id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Reminder>> GetInRangeAsync(
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Reminder>> GetDueAsync(
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default);

    Task UpsertAsync(Reminder reminder, CancellationToken cancellationToken = default);
}
