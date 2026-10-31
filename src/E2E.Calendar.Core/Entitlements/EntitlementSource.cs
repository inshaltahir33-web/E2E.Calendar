namespace E2E.Calendar.Core.Entitlements;

public enum EntitlementSource
{
    /// <summary>No source has been checked yet.</summary>
    None = 0,

    /// <summary>Read from the Microsoft Store license.</summary>
    Store = 1,

    /// <summary>Last known value from the local cache because the Store is unreachable.</summary>
    Cache = 2,

    /// <summary>Free tier fallback; the app is not Store-associated.</summary>
    Fallback = 3,
}
