namespace E2E.Calendar.Core.Entitlements;

/// <summary>
/// Microsoft Store product IDs. Replace each placeholder with the add-on's
/// "product ID" (in-app offer token) from Partner Center before publishing.
/// </summary>
public static class StoreProductIds
{
    public const string Monthly = "e2e_calendar_monthly";

    public const string Yearly = "e2e_calendar_yearly";

    public const string Lifetime = "e2e_calendar_lifetime";

    public static IReadOnlyList<string> All { get; } = [Monthly, Yearly, Lifetime];
}
