using E2E.Calendar.Core.Abstractions;
using E2E.Calendar.Core.Entitlements;
using E2E.Calendar.Core.Services;
using Microsoft.Extensions.Logging;
using Windows.Services.Store;

namespace E2E.Calendar.Platform;

/// <summary>
/// Microsoft Store licensing. Falls back to the cached entitlement when the Store
/// is unreachable and to <see cref="FreeEntitlementService"/> when the app is not
/// Store-associated (sideloaded or unpackaged).
/// </summary>
public sealed class StoreEntitlementService : IEntitlementService
{
    private static readonly string[] AddOnKinds = ["Durable"];

    private readonly IClock _clock;
    private readonly FreeEntitlementService _fallback;
    private readonly ILogger<StoreEntitlementService> _logger;
    private readonly object _gate = new();

    private StoreContext? _context;
    private EntitlementSnapshot _current;

    public StoreEntitlementService(IClock clock, FreeEntitlementService fallback, ILogger<StoreEntitlementService> logger)
    {
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(fallback);
        ArgumentNullException.ThrowIfNull(logger);

        _clock = clock;
        _fallback = fallback;
        _logger = logger;
        _current = EntitlementSnapshot.Free(clock.UtcNow);
    }

    public bool IsStoreAvailable => _context is not null;

    public EntitlementSnapshot Current
    {
        get
        {
            lock (_gate)
            {
                return _current;
            }
        }
    }

    public event EventHandler<EntitlementSnapshot>? EntitlementChanged;

    /// <summary>
    /// Creates the Store context for this window. Call once after the main window exists;
    /// Store purchase UI is parented to <paramref name="windowHandle"/>.
    /// </summary>
    public async Task InitializeAsync(nint windowHandle, CancellationToken cancellationToken = default)
    {
        if (_context is not null)
        {
            return;
        }

        try
        {
            var context = StoreContext.GetDefault();
            WinRT.Interop.InitializeWithWindow.Initialize(context, windowHandle);

            context.OfflineLicensesChanged += OnOfflineLicensesChanged;
            _context = context;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Microsoft Store licensing is unavailable; using the free fallback.");
            await ApplyAsync(await LoadFallbackAsync(cancellationToken));
            return;
        }

        await RefreshAsync(cancellationToken);
    }

    public bool Can(Feature feature) => Current.IsPremium || _fallback.Can(feature);

    public int LimitFor(Feature feature) => Current.IsPremium ? -1 : _fallback.LimitFor(feature);

    public async Task<EntitlementSnapshot> RefreshAsync(CancellationToken cancellationToken = default)
    {
        var context = _context;

        if (context is null)
        {
            return await ApplyAsync(await LoadFallbackAsync(cancellationToken));
        }

        try
        {
            var license = await context.GetAppLicenseAsync();
            cancellationToken.ThrowIfCancellationRequested();

            return await ApplyAsync(MapLicense(license, _clock.UtcNow), cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not read the Store license; keeping the last known entitlement.");
            return await ApplyAsync(await LoadFallbackAsync(cancellationToken));
        }
    }

    public async Task<IReadOnlyList<StoreProductInfo>> GetProductsAsync(CancellationToken cancellationToken = default)
    {
        var context = _context;

        if (context is null)
        {
            return [];
        }

        try
        {
            var collection = await context.GetAssociatedStoreProductsAsync(AddOnKinds);
            cancellationToken.ThrowIfCancellationRequested();

            var products = new List<StoreProductInfo>(StoreProductIds.All.Count);

            foreach (var productId in StoreProductIds.All)
            {
                var product = collection.Products.Values.FirstOrDefault(item =>
                    item.InAppOfferToken == productId || item.StoreId == productId);

                if (product is null)
                {
                    continue;
                }

                var sku = product.Skus.FirstOrDefault(item => item.IsSubscription) ?? product.Skus.FirstOrDefault();
                var subscription = sku is { IsSubscription: true } ? sku.SubscriptionInfo : null;
                var priceText = subscription is not null
                    ? FirstNonEmpty(sku!.Price?.FormattedRecurrencePrice, sku.Price?.FormattedPrice, product.Price?.FormattedPrice)
                    : FirstNonEmpty(product.Price?.FormattedPrice, sku?.Price?.FormattedPrice);
                var owned = sku?.IsInUserCollection == true || product.IsInUserCollection;

                products.Add(new StoreProductInfo(
                    productId,
                    string.IsNullOrWhiteSpace(product.Title) ? FallbackTitle(productId) : product.Title,
                    priceText ?? string.Empty,
                    subscription is not null,
                    TrialDays(subscription),
                    owned));
            }

            return products;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not read the premium products from the Store.");
            return [];
        }
    }

    public async Task<PurchaseResult> PurchaseAsync(string productId, CancellationToken cancellationToken = default)
    {
        if (!StoreProductIds.All.Contains(productId))
        {
            return new PurchaseResult(PurchaseStatus.NotAvailable, Current, "Unknown product.");
        }

        var context = _context;

        if (context is null)
        {
            return new PurchaseResult(
                PurchaseStatus.NotAvailable,
                Current,
                "Install the Microsoft Store version of the app to buy premium.");
        }

        try
        {
            // Config holds Partner Center product IDs (in-app offer tokens); the purchase
            // call itself needs the catalog Store ID of that product.
            var product = await FindProductAsync(context, productId, cancellationToken);

            if (product is null || string.IsNullOrWhiteSpace(product.StoreId))
            {
                return new PurchaseResult(PurchaseStatus.NotAvailable, Current, "This plan isn't available in your market.");
            }

            // RequestPurchaseAsync shows Store UI, so it must run on the UI thread (the paywall button).
            var purchase = await context.RequestPurchaseAsync(product.StoreId);
            cancellationToken.ThrowIfCancellationRequested();

            switch (purchase.Status)
            {
                case StorePurchaseStatus.Succeeded:
                    return new PurchaseResult(PurchaseStatus.Succeeded, await RefreshAsync(cancellationToken));

                case StorePurchaseStatus.AlreadyPurchased:
                    return new PurchaseResult(PurchaseStatus.AlreadyOwned, await RefreshAsync(cancellationToken));

                case StorePurchaseStatus.NotPurchased:
                    return new PurchaseResult(PurchaseStatus.Canceled, Current, "Purchase canceled.");

                case StorePurchaseStatus.NetworkError:
                    return new PurchaseResult(
                        PurchaseStatus.Offline,
                        Current,
                        "The Store could not be reached. Check your connection and try again.");

                default:
                    return new PurchaseResult(PurchaseStatus.Error, Current, "The Store reported an error. Try again later.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Purchase of {ProductId} failed.", productId);
            return new PurchaseResult(PurchaseStatus.Error, Current, "The purchase could not be completed.");
        }
    }

    public async Task<PurchaseResult> RestoreAsync(CancellationToken cancellationToken = default)
    {
        var context = _context;

        if (context is null)
        {
            return new PurchaseResult(
                PurchaseStatus.NotAvailable,
                Current,
                "Install the Microsoft Store version of the app to restore purchases.");
        }

        try
        {
            // Re-syncs owned add-ons with the Store; the refreshed license then reflects them.
            await context.GetUserCollectionAsync(AddOnKinds);
            cancellationToken.ThrowIfCancellationRequested();

            var snapshot = await RefreshAsync(cancellationToken);

            return snapshot.IsPremium
                ? new PurchaseResult(PurchaseStatus.Succeeded, snapshot, "Your premium plan is active.")
                : new PurchaseResult(PurchaseStatus.Succeeded, snapshot, "No previous purchases were found for this account.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Restore purchases failed.");
            return new PurchaseResult(
                PurchaseStatus.Offline,
                Current,
                "Purchases could not be restored. Check your connection and try again.");
        }
    }

    private void OnOfflineLicensesChanged(StoreContext sender, object args) => _ = RefreshAsync();

    private static async Task<StoreProduct?> FindProductAsync(
        StoreContext context,
        string productId,
        CancellationToken cancellationToken)
    {
        var collection = await context.GetAssociatedStoreProductsAsync(AddOnKinds);
        cancellationToken.ThrowIfCancellationRequested();

        return collection.Products.Values.FirstOrDefault(item =>
            item.InAppOfferToken == productId || item.StoreId == productId);
    }

    private async Task<EntitlementSnapshot> ApplyAsync(EntitlementSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            _current = snapshot;
        }

        if (snapshot.Source == EntitlementSource.Store)
        {
            await EntitlementCache.SaveAsync(snapshot, cancellationToken);
        }

        EntitlementChanged?.Invoke(this, snapshot);

        return snapshot;
    }

    private async Task<EntitlementSnapshot> LoadFallbackAsync(CancellationToken cancellationToken)
    {
        var cached = await EntitlementCache.LoadAsync(_clock.UtcNow, cancellationToken);

        return cached ?? await _fallback.RefreshAsync(cancellationToken);
    }

    private static EntitlementSnapshot MapLicense(StoreAppLicense license, DateTimeOffset nowUtc)
    {
        var addOns = license.AddOnLicenses;

        if (addOns.TryGetValue(StoreProductIds.Lifetime, out var lifetime) && IsUsable(lifetime, nowUtc))
        {
            return new EntitlementSnapshot(true, EntitlementTier.Lifetime, EntitlementSource.Store, null, license.IsTrial, nowUtc);
        }

        if (addOns.TryGetValue(StoreProductIds.Yearly, out var yearly) && IsUsable(yearly, nowUtc))
        {
            return new EntitlementSnapshot(true, EntitlementTier.Yearly, EntitlementSource.Store, yearly.ExpirationDate, license.IsTrial, nowUtc);
        }

        if (addOns.TryGetValue(StoreProductIds.Monthly, out var monthly) && IsUsable(monthly, nowUtc))
        {
            return new EntitlementSnapshot(true, EntitlementTier.Monthly, EntitlementSource.Store, monthly.ExpirationDate, license.IsTrial, nowUtc);
        }

        return new EntitlementSnapshot(false, EntitlementTier.Free, EntitlementSource.Store, null, false, nowUtc);
    }

    private static bool IsUsable(StoreLicense license, DateTimeOffset nowUtc) =>
        license.IsActive && license.ExpirationDate > nowUtc;

    private static int TrialDays(StoreSubscriptionInfo? info)
    {
        if (info is null || !info.HasTrialPeriod)
        {
            return 0;
        }

        var value = (int)info.TrialPeriod;

        return info.TrialPeriodUnit switch
        {
            StoreDurationUnit.Week => value * 7,
            StoreDurationUnit.Month => value * 30,
            StoreDurationUnit.Year => value * 365,
            _ => value,
        };
    }

    private static string? FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

    private static string FallbackTitle(string productId) => productId switch
    {
        StoreProductIds.Monthly => "Monthly",
        StoreProductIds.Yearly => "Yearly",
        StoreProductIds.Lifetime => "Lifetime",
        _ => productId,
    };
}
