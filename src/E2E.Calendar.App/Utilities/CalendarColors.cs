using System.Collections.Concurrent;
using E2E.Calendar.Core.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.UI.ViewManagement;

namespace E2E.Calendar.App.Utilities;

/// <summary>
/// Resolves palette names to the theme brushes declared in Themes/Tokens.xaml
/// (<c>Cal.{Name}.BarBrush</c> / <c>FillBrush</c> / <c>TextBrush</c>). No colour
/// literal ever reaches the UI layer.
/// </summary>
public static class CalendarColors
{
    private static readonly ConcurrentDictionary<string, Brush> Cache = new();
    private static readonly AccessibilitySettings Accessibility = new();

    /// <summary>Fallback theme used by converters, which have no element to inspect.</summary>
    public static ElementTheme Theme { get; set; } = ElementTheme.Default;

    public static Brush Bar(FrameworkElement? element, string? color) => Resolve(element, color, "Bar");

    public static Brush Fill(FrameworkElement? element, string? color) => Resolve(element, color, "Fill");

    public static Brush Text(FrameworkElement? element, string? color) => Resolve(element, color, "Text");

    public static Brush Resolve(FrameworkElement? element, string? color, string role)
    {
        var themeKey = ThemeKey(element);

        return Lookup(themeKey, color, role)
            ?? Lookup(themeKey, CalendarPalette.Fallback, role)
            ?? Lookup("Light", CalendarPalette.Fallback, role)
            ?? Transparent;
    }

    private static Brush Transparent { get; } = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));

    private static Brush? Lookup(string themeKey, string? color, string role)
    {
        var name = CalendarPalette.Normalize(color);
        var key = $"Cal.{name}.{role}Brush";
        var cacheKey = themeKey + "|" + key;

        if (Cache.TryGetValue(cacheKey, out var cached))
        {
            return cached;
        }

        var brush = FromThemeDictionary(themeKey, key);

        if (brush is not null)
        {
            Cache[cacheKey] = brush;
        }

        return brush;
    }

    private static Brush? FromThemeDictionary(string themeKey, string key)
    {
        if (Application.Current?.Resources is { } resources)
        {
            if (resources.ThemeDictionaries.TryGetValue(themeKey, out var value) &&
                value is ResourceDictionary dictionary &&
                dictionary.TryGetValue(key, out var brush) &&
                brush is Brush themed)
            {
                return themed;
            }

            if (resources.TryGetValue(key, out var fallback) && fallback is Brush direct)
            {
                return direct;
            }
        }

        return null;
    }

    private static string ThemeKey(FrameworkElement? element)
    {
        try
        {
            if (Accessibility.HighContrast)
            {
                return "HighContrast";
            }
        }
        catch (Exception)
        {
            // AccessibilitySettings can throw outside an interactive session.
        }

        var theme = element?.ActualTheme ?? ElementTheme.Default;

        if (theme == ElementTheme.Default)
        {
            theme = Theme;
        }

        return theme switch
        {
            ElementTheme.Light => "Light",
            ElementTheme.Dark => "Dark",
            _ => SystemThemeKey(),
        };
    }

    private static string SystemThemeKey()
    {
        try
        {
            var background = new UISettings().GetColorValue(UIColorType.Background);
            return background.R > 128 ? "Light" : "Dark";
        }
        catch (Exception)
        {
            return "Light";
        }
    }
}
