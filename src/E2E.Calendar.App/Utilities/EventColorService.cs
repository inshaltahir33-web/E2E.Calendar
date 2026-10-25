using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using E2E.Calendar.Core.Models;
using Windows.UI;

namespace E2E.Calendar.App.Utilities;

public sealed record EventChipBrushes(Brush Fill, Brush Text, Brush Stripe);

public static class EventColorService
{
    public static EventChipBrushes GetChipBrushes(string? calendarColor, ElementTheme theme)
    {
        var source = ResolveSource(calendarColor, theme);
        var surface = ResourceColor(theme, "App.SurfaceRaisedBrush");
        var fillAlpha = theme == ElementTheme.Dark ? 0.28 : 0.16;
        var fill = Blend(source, surface, fillAlpha);
        var text = theme == ElementTheme.Dark
            ? Blend(source, ResourceColor(theme, "App.ContrastLightBrush"), 0.55)
            : Blend(source, ResourceColor(theme, "App.ContrastDarkBrush"), 0.45);

        text = EnsureContrast(text, fill, theme == ElementTheme.Dark);

        return new EventChipBrushes(
            ColorHelper.FromColor(fill),
            ColorHelper.FromColor(text),
            ColorHelper.FromColor(source));
    }

    private static Color ResolveSource(string? value, ElementTheme theme)
    {
        if (ColorHelper.TryParse(value, out var parsed))
        {
            return Color.FromArgb(255, parsed.R, parsed.G, parsed.B);
        }

        var themeKey = theme == ElementTheme.Dark ? "Dark" : "Light";
        var key = $"Cal.{CalendarPalette.Normalize(value)}.BarBrush";
        if (ThemeBrushes.TryGetThemed(themeKey, key, out var themedBrush)
            && themedBrush is SolidColorBrush brush)
        {
            return brush.Color;
        }

        return ResourceColor(theme, "App.AccentFillBrush");
    }

    private static Color ResourceColor(ElementTheme theme, string key)
    {
        var themeKey = theme == ElementTheme.Dark ? "Dark" : theme == ElementTheme.Light ? "Light" : "Light";
        if (ThemeBrushes.TryGetThemed(themeKey, key, out var themedValue)
            && themedValue is SolidColorBrush brush)
        {
            return brush.Color;
        }

        if (Application.Current?.Resources.TryGetValue(key, out var direct) == true
            && direct is SolidColorBrush directBrush)
        {
            return directBrush.Color;
        }

        return ResourceColor(theme, "App.TextPrimaryBrush");
    }

    private static Color Blend(Color foreground, Color background, double amount)
    {
        return Color.FromArgb(
            255,
            (byte)(foreground.R + ((background.R - foreground.R) * amount)),
            (byte)(foreground.G + ((background.G - foreground.G) * amount)),
            (byte)(foreground.B + ((background.B - foreground.B) * amount)));
    }

    private static Color EnsureContrast(Color foreground, Color background, bool preferLight)
    {
        var candidate = foreground;
        for (var index = 0; index < 20 && ContrastRatio(candidate, background) < 4.5; index++)
        {
            candidate = Blend(
                candidate,
                preferLight
                    ? Color.FromArgb(255, 255, 255, 255)
                    : Color.FromArgb(255, 0, 0, 0),
                0.12);
        }

        return candidate;
    }

    private static double ContrastRatio(Color first, Color second)
    {
        var firstLuminance = RelativeLuminance(first);
        var secondLuminance = RelativeLuminance(second);
        var lighter = Math.Max(firstLuminance, secondLuminance);
        var darker = Math.Min(firstLuminance, secondLuminance);
        return (lighter + 0.05) / (darker + 0.05);
    }

    private static double RelativeLuminance(Color color)
    {
        static double Channel(byte channel)
        {
            var value = channel / 255d;
            return value <= 0.03928 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4);
        }

        return (0.2126 * Channel(color.R)) + (0.7152 * Channel(color.G)) + (0.0722 * Channel(color.B));
    }
}
