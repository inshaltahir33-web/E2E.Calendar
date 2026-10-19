using E2E.Calendar.Core.Models;

namespace E2E.Calendar.Core.Abstractions;

public interface IAlertRepository
{
    Task<Alert?> GetAlertByIdAsync(string id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Alert>> GetAlertsForOwnerAsync(
        OwnerType ownerType,
        string ownerId,
        CancellationToken cancellationToken = default);

    Task UpsertAlertAsync(Alert alert, CancellationToken cancellationToken = default);

    Task DeleteAlertAsync(string id, CancellationToken cancellationToken = default);

    Task<AlertInstance?> GetInstanceByIdAsync(string id, CancellationToken cancellationToken = default);

    Task UpsertInstanceAsync(AlertInstance instance, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AlertInstance>> GetPendingInstancesAsync(
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default);

    Task<AlertInstance?> PeekNextPendingInstanceAsync(CancellationToken cancellationToken = default);
}
