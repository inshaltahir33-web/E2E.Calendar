using E2E.Calendar.Core.Entitlements;

namespace E2E.Calendar.App.Services;

public static class EntitlementText
{
    public static string Describe(EntitlementSnapshot snapshot, bool storeAvailable)
    {
        if (!snapshot.IsPremium)
        {
            return storeAvailable
                ? "You're on the free plan."
                : "The Microsoft Store isn't available in this build, so premium purchases are disabled.";
        }

        var plan = snapshot.Tier switch
        {
            EntitlementTier.Monthly => "Monthly",
            EntitlementTier.Yearly => "Yearly",
            EntitlementTier.Lifetime => "Lifetime",
            _ => "Premium",
        };

        if (snapshot.IsTrial)
        {
            return $"Premium trial active · {plan}.";
        }

        if (snapshot.Tier != EntitlementTier.Lifetime && snapshot.ExpiresUtc is { } expires)
        {
            return $"Premium is active · {plan}, renews {expires.ToLocalTime():d MMM yyyy}.";
        }

        return $"Premium is active · {plan}.";
    }
}
