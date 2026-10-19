using E2E.Calendar.Core.Sync;

namespace E2E.Calendar.Core.Abstractions;

public interface IOutbox
{
    Task EnqueueAsync(OutboxItem item, CancellationToken cancellationToken = default);
}
