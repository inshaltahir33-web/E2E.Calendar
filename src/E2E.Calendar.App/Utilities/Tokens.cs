using Microsoft.UI.Xaml;

namespace E2E.Calendar.App.Utilities;

public static class Tokens
{
    public static double Double(string key, double fallback = 0) =>
        Application.Current.Resources.TryGetValue(key, out var value) && value is double number
            ? number
            : fallback;

    public static Thickness Thickness(string key, Thickness fallback = default) =>
        Application.Current.Resources.TryGetValue(key, out var value) && value is Thickness thickness
            ? thickness
            : fallback;

    public static CornerRadius CornerRadius(string key, double fallback = 0) =>
        Application.Current.Resources.TryGetValue(key, out var value) && value is CornerRadius radius
            ? radius
            : new CornerRadius(fallback);
}
