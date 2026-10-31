using E2E.Calendar.Core.Abstractions;
using E2E.Calendar.Core.Entitlements;

namespace E2E.Calendar.Core.Services;

/// <summary>
/// Fallback used when the app is not Store-associated or the Store is unreachable.
/// Everything stays unlocked, matching the pre-licensing behaviour.
/// </summary>
public sealed class FreeEntitlementService : IEntitlementService
{
    private EntitlementSnapshot _current = EntitlementSnapshot.Free(DateTimeOffset.UtcNow);

    public bool Can(Feature feature) => true;

    public int LimitFor(Feature feature) => -1;

    public bool IsStoreAvailable => false;

    public EntitlementSnapshot Current => _current;

    public event EventHandler<EntitlementSnapshot>? EntitlementChanged;

    public Task<EntitlementSnapshot> RefreshAsync(CancellationToken cancellationToken = default)
    {
        _current = EntitlementSnapshot.Free(DateTimeOffset.UtcNow);
        EntitlementChanged?.Invoke(this, _current);

        return Task.FromResult(_current);
    }

    public Task<IReadOnlyList<StoreProductInfo>> GetProductsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<StoreProductInfo>>([]);

    public Task<PurchaseResult> PurchaseAsync(string productId, CancellationToken cancellationToken = default) =>
        Task.FromResult(new PurchaseResult(
            PurchaseStatus.NotAvailable,
            _current,
            "Purchases need the Microsoft Store version of the app."));

    public Task<PurchaseResult> RestoreAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new PurchaseResult(
            PurchaseStatus.NotAvailable,
            _current,
            "Purchases need the Microsoft Store version of the app."));
}
