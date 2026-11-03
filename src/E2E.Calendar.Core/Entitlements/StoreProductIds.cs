namespace E2E.Calendar.Core.Entitlements;

/// <summary>
/// Microsoft Store add-on identifiers. These values are placeholders: real IDs must
/// come from Partner Center before enabling premium (AppFeatures.PremiumUiEnabled).
/// While premium is disabled the Store service never sends them to the Store.
/// </summary>
public static class StoreProductIds
{
    public const string Monthly = "e2e_calendar_monthly";

    public const string Yearly = "e2e_calendar_yearly";

    public const string Lifetime = "e2e_calendar_lifetime";

    public static IReadOnlyList<string> All { get; } = [Monthly, Yearly, Lifetime];
}
