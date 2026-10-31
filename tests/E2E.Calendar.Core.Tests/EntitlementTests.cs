using E2E.Calendar.Core.Entitlements;
using E2E.Calendar.Core.Services;
using FluentAssertions;
using Xunit;

namespace E2E.Calendar.Core.Tests;

public sealed class EntitlementTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 31, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Free_snapshot_is_not_premium_and_has_no_expiry()
    {
        var snapshot = EntitlementSnapshot.Free(Now);

        snapshot.IsPremium.Should().BeFalse();
        snapshot.Tier.Should().Be(EntitlementTier.Free);
        snapshot.Source.Should().Be(EntitlementSource.Fallback);
        snapshot.ExpiresUtc.Should().BeNull();
        snapshot.CheckedUtc.Should().Be(Now);
    }

    [Fact]
    public void Free_fallback_reports_no_store()
    {
        var service = new FreeEntitlementService();

        service.IsStoreAvailable.Should().BeFalse();
        service.Current.IsPremium.Should().BeFalse();
        service.Current.Source.Should().Be(EntitlementSource.Fallback);
    }

    [Fact]
    public async Task Free_fallback_offers_no_products_and_rejects_purchases()
    {
        var service = new FreeEntitlementService();

        (await service.GetProductsAsync()).Should().BeEmpty();

        var purchase = await service.PurchaseAsync(StoreProductIds.Monthly);
        purchase.Status.Should().Be(PurchaseStatus.NotAvailable);
        purchase.Entitlement.IsPremium.Should().BeFalse();

        var restore = await service.RestoreAsync();
        restore.Status.Should().Be(PurchaseStatus.NotAvailable);
        restore.Entitlement.IsPremium.Should().BeFalse();
    }

    [Fact]
    public void Product_ids_are_distinct_placeholders()
    {
        StoreProductIds.All.Should().OnlyHaveUniqueItems().And.HaveCount(3);
        StoreProductIds.All.Should().OnlyContain(id => !string.IsNullOrWhiteSpace(id));
        StoreProductIds.All.Should().BeEquivalentTo(
            new[] { StoreProductIds.Monthly, StoreProductIds.Yearly, StoreProductIds.Lifetime });
    }
}
