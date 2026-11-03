namespace E2E.Calendar.App;

/// <summary>
/// Build/runtime feature switches. Flip a flag to re-enable a hidden surface;
/// no sync code is removed when a flag is off.
/// </summary>
public static class AppFeatures
{
    public static bool SyncUiEnabled = false;

    /// <summary>
    /// Shows the Premium settings section and billing entry points. Off by default so
    /// the whole app is free; billing code stays compiled and reachable by flipping it.
    /// </summary>
    public static bool PremiumUiEnabled = false;

    public static bool DemoEnabled = string.Equals(
        Environment.GetEnvironmentVariable("E2E_DEMO"),
        "1",
        StringComparison.Ordinal);
}
