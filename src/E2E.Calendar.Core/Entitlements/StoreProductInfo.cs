namespace E2E.Calendar.Core.Entitlements;

/// <summary>A purchasable premium plan, with pricing already formatted for the user's market.</summary>
public sealed record StoreProductInfo(
    string ProductId,
    string Title,
    string PriceText,
    bool IsSubscription,
    int TrialDays,
    bool IsOwned);
