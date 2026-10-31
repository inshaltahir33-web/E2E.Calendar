namespace E2E.Calendar.Core.Entitlements;

public sealed record EntitlementSnapshot(
    bool IsPremium,
    EntitlementTier Tier,
    EntitlementSource Source,
    DateTimeOffset? ExpiresUtc,
    bool IsTrial,
    DateTimeOffset CheckedUtc)
{
    public static EntitlementSnapshot Free(DateTimeOffset checkedUtc) =>
        new(false, EntitlementTier.Free, EntitlementSource.Fallback, null, false, checkedUtc);
}
