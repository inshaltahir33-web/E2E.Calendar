using Windows.ApplicationModel;

namespace E2E.Calendar.App;

/// <summary>
/// Store-facing identity, contact and link values shown in the About card.
/// Change metadata here only; the views bind to these values.
/// </summary>
public static class AppInfo
{
    public const string DisplayName = "Calendar Pro";
    public const string Developer = "BindLabs";
    public const string SupportEmail = "bindlabs65@gmail.com";
    public const string PrivacyPolicyUrl = "https://sites.google.com/view/bindlabs/privacy-policy";
    public const string TermsOfUseUrl = "https://sites.google.com/view/bindlabs/terms-of-use";
    public const string StoreProductId = "9N75PKS2KLBJ";

    /// <summary>
    /// Remote holiday override feed. This is still a placeholder: Set real URL before enabling.
    /// While it is blank or a placeholder, no request is made and bundled holiday ICS files are used.
    /// </summary>
    public const string HolidayOverridesUrl = "https://raw.githubusercontent.com/OWNER/REPO/main/holiday-overrides.json";

    public static string SupportUri => $"mailto:{SupportEmail}";

    public static string RateUri => $"ms-windows-store://review/?ProductId={StoreProductId}";

    public static string StoreUri => $"https://apps.microsoft.com/detail/{StoreProductId}";

    /// <summary>
    /// The package version when packaged; the assembly version during local (unpackaged) runs.
    /// </summary>
    public static string Version
    {
        get
        {
            try
            {
                var version = Package.Current.Id.Version;
                return $"{version.Major}.{version.Minor}.{version.Build}.{version.Revision}";
            }
            catch (Exception)
            {
                var version = typeof(AppInfo).Assembly.GetName().Version;
                return version?.ToString() ?? "0.0.0.0";
            }
        }
    }
}
