namespace E2E.Calendar.Core.Entitlements;

public enum PurchaseStatus
{
    Succeeded = 0,
    AlreadyOwned = 1,
    Canceled = 2,
    NotAvailable = 3,
    Offline = 4,
    Error = 5,
}

public sealed record PurchaseResult(PurchaseStatus Status, EntitlementSnapshot Entitlement, string? Message = null);
