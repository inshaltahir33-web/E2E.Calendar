namespace E2E.Calendar.App;

/// <summary>
/// Build/runtime feature switches. Flip a flag to re-enable a hidden surface;
/// no sync code is removed when a flag is off.
/// </summary>
public static class AppFeatures
{
    public static bool SyncUiEnabled = false;

    public static string PrivacyPolicyUrl = "https://example.com/privacy";

    public static bool DemoEnabled = string.Equals(
        Environment.GetEnvironmentVariable("E2E_DEMO"),
        "1",
        StringComparison.Ordinal);
}
