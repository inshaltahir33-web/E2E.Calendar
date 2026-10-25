using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.UI.ViewManagement;

namespace E2E.Calendar.App.Utilities;

/// <summary>
/// Theme-aware brush lookup for code-behind.
/// Application.Current.Resources["App.XBrush"] resolves against the OS/app theme and ignores the
/// per-window RequestedTheme override, so when the window is Dark but Windows is Light the code-built
/// UI (week header, gutter, weekend columns, caption buttons) picked Light brushes.
/// This helper resolves against the owner element's ActualTheme instead.
/// </summary>
internal static class ThemeBrushes
{
    private static readonly AccessibilitySettings Accessibility = new();

    public static Brush Get(FrameworkElement owner, string key)
    {
        ArgumentNullException.ThrowIfNull(owner);
        return Get(owner.ActualTheme, key);
    }

    public static Brush Get(ElementTheme theme, string key)
    {
        var resources = Application.Current.Resources;

        if (!IsHighContrast() && theme != ElementTheme.Default)
        {
            var themeKey = theme == ElementTheme.Dark ? "Dark" : "Light";

            if (TryFind(resources, themeKey, key, out var themed))
            {
                return themed;
            }
        }

        if (resources.TryGetValue(key, out var fallback) && fallback is Brush direct)
        {
            return direct;
        }

        return new SolidColorBrush();
    }

    /// <summary>Looks a brush up in the Light/Dark theme dictionaries, including merged dictionaries.</summary>
    public static bool TryGetThemed(string themeKey, string key, out Brush brush) =>
        TryFind(Application.Current.Resources, themeKey, key, out brush);

    private static bool TryFind(ResourceDictionary dictionary, string themeKey, string key, out Brush brush)
    {
        if (dictionary.ThemeDictionaries.TryGetValue(themeKey, out var value)
            && value is ResourceDictionary themeDictionary
            && themeDictionary.TryGetValue(key, out var found)
            && found is Brush foundBrush)
        {
            brush = foundBrush;
            return true;
        }

        foreach (var merged in dictionary.MergedDictionaries)
        {
            if (TryFind(merged, themeKey, key, out brush))
            {
                return true;
            }
        }

        brush = null!;
        return false;
    }

    private static bool IsHighContrast()
    {
        try
        {
            return Accessibility.HighContrast;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
