using E2E.Calendar.Core.Models;

namespace E2E.Calendar.Core.Abstractions;

public interface IAlertRepository
{
    Task<Alert?> GetAlertByIdAsync(string id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Alert>> GetAlertsForOwnerAsync(
        OwnerType ownerType,
        string ownerId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Alert>> GetAllAlertsAsync(CancellationToken cancellationToken = default);

    Task UpsertAlertAsync(Alert alert, CancellationToken cancellationToken = default);

    Task DeleteAlertAsync(string id, CancellationToken cancellationToken = default);

    Task<AlertInstance?> GetInstanceByIdAsync(string id, CancellationToken cancellationToken = default);

    Task UpsertInstanceAsync(AlertInstance instance, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AlertInstance>> GetPendingInstancesAsync(
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AlertInstance>> GetPendingInstancesForSchedulingAsync(
        int limit,
        CancellationToken cancellationToken = default);

    Task<bool> TryMarkFiredAsync(
        string instanceId,
        DateTimeOffset firedUtc,
        CancellationToken cancellationToken = default);

    Task MarkMissedAsync(
        string instanceId,
        DateTimeOffset missedUtc,
        CancellationToken cancellationToken = default);

    Task MarkSnoozedAsync(
        string instanceId,
        DateTimeOffset snoozedUntilUtc,
        CancellationToken cancellationToken = default);

    Task MarkDoneAsync(
        string instanceId,
        DateTimeOffset doneUtc,
        CancellationToken cancellationToken = default);

    Task DeleteStalePendingInstancesAsync(
        string alertId,
        IReadOnlyList<string> keepInstanceIds,
        CancellationToken cancellationToken = default);

    Task<AlertInstance?> PeekNextPendingInstanceAsync(CancellationToken cancellationToken = default);
}
