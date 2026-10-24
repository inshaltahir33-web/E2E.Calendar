using E2E.Calendar.Core.Models;

namespace E2E.Calendar.Core.Abstractions;

public interface IAttachmentRepository
{
    Task<IReadOnlyList<EventAttachment>> GetByEventIdAsync(string eventId, CancellationToken cancellationToken = default);

    Task UpsertAsync(EventAttachment attachment, CancellationToken cancellationToken = default);

    Task DeleteAsync(string id, CancellationToken cancellationToken = default);
}
