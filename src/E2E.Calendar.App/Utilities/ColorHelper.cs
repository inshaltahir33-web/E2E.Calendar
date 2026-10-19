using System.Globalization;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace E2E.Calendar.App.Utilities;

public static class ColorHelper
{
    public static Brush FromHex(string? hex)
    {
        if (!TryParse(hex, out var color))
        {
            return new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));
        }

        return new SolidColorBrush(color);
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
