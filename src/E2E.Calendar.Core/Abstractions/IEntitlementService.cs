using E2E.Calendar.Core.Entitlements;

namespace E2E.Calendar.Core.Abstractions;

public interface IEntitlementService
{
    bool Can(Feature feature);

    int LimitFor(Feature feature);

    /// <summary>True when Microsoft Store licensing is available in this build/session.</summary>
    bool IsStoreAvailable { get; }

    /// <summary>Last known entitlement; never blocks on I/O.</summary>
    EntitlementSnapshot Current { get; }

    event EventHandler<EntitlementSnapshot>? EntitlementChanged;

    /// <summary>Re-reads the license from the Store, falling back to the cache when offline.</summary>
    Task<EntitlementSnapshot> RefreshAsync(CancellationToken cancellationToken = default);

    /// <summary>Returns the purchasable premium plans in this market. Empty when the Store is unavailable.</summary>
    Task<IReadOnlyList<StoreProductInfo>> GetProductsAsync(CancellationToken cancellationToken = default);

    Task<PurchaseResult> PurchaseAsync(string productId, CancellationToken cancellationToken = default);

    /// <summary>Re-syncs purchases for the signed-in Store account and refreshes the entitlement.</summary>
    Task<PurchaseResult> RestoreAsync(CancellationToken cancellationToken = default);
}
