namespace E2E.Calendar.Core.Entitlements;

public static class FreeTier
{
    public const int MaxAccounts = 1;

    public const int MaxAlertsPerOwner = 3;

    public const int MaxTaskLists = 1;

    public const int MaxTemplates = 2;

    public const int Unlimited = -1;

    public static int LimitFor(Feature feature) => feature switch
    {
        Feature.MultipleAccounts => MaxAccounts,
        Feature.MultipleTaskLists => MaxTaskLists,
        Feature.UnlimitedAlerts => MaxAlertsPerOwner,
        Feature.CalendarTemplates => MaxTemplates,
        Feature.CalendarSets => Unlimited,
        Feature.FreeSlotFinder => Unlimited,
        _ => Unlimited,
    };
}
