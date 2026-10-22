using System.Globalization;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace E2E.Calendar.App.Utilities;

public static class ColorHelper
{
    private const int MaxCachedBrushes = 128;

    private static readonly Dictionary<uint, SolidColorBrush> BrushCache = new();

    public static Brush FromHex(string? hex)
    {
        if (!TryParse(hex, out var color))
        {
            return Transparent;
        }

        return FromColor(color);
    }

    public static SolidColorBrush Transparent => FromColor(Color.FromArgb(0, 0, 0, 0));

    /// <summary>
    /// Brushes are immutable and shareable, so a small palette-wide cache avoids
    /// allocating a new brush for every chip that is rendered or hovered.
    /// </summary>
    public static SolidColorBrush FromColor(Color color)
    {
        var key = ((uint)color.A << 24) | ((uint)color.R << 16) | ((uint)color.G << 8) | color.B;

        lock (BrushCache)
        {
            if (BrushCache.TryGetValue(key, out var cached))
            {
                return cached;
            }

            if (BrushCache.Count >= MaxCachedBrushes)
            {
                BrushCache.Clear();
            }

            var brush = new SolidColorBrush(color);

            BrushCache[key] = brush;

            return brush;
        }
    }

    public static bool TryParse(string? hex, out Color color)
    {
        color = Color.FromArgb(0, 0, 0, 0);

        if (string.IsNullOrWhiteSpace(hex))
        {
            return false;
        }

        var value = hex.Trim().TrimStart('#');

        if (value.Length == 6)
        {
            value = "FF" + value;
        }

        if (value.Length != 8 ||
            !uint.TryParse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var argb))
        {
            return false;
        }

        color = Color.FromArgb(
            (byte)(argb >> 24),
            (byte)(argb >> 16),
            (byte)(argb >> 8),
            (byte)argb);

        return true;
    }
}
