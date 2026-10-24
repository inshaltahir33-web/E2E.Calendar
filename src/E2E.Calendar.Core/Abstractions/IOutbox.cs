using E2E.Calendar.Core.Sync;

namespace E2E.Calendar.Core.Abstractions;

public interface IOutbox
{
    Task EnqueueAsync(OutboxItem item, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<OutboxItem>> GetPendingAsync(int limit = 100, CancellationToken cancellationToken = default);

    Task RemoveAsync(long id, CancellationToken cancellationToken = default);
}
