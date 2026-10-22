using E2E.Calendar.Core.Models;
using E2E.Calendar.Core.Services;
using Microsoft.UI.Xaml;
using Windows.UI.ViewManagement;

namespace E2E.Calendar.App.Services;

/// <summary>
/// Applies <see cref="AppSettings"/> to the running UI: theme, accent colour and
/// grid metrics. The theme is applied to every registered window root because
/// <see cref="Application.RequestedTheme"/> can only be set at startup in WinUI 3.
/// </summary>
public sealed class AppearanceService
{
    private readonly SettingsService _settings;
    private readonly List<WeakReference<FrameworkElement>> _targets = new();

    public AppearanceService(SettingsService settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        _settings = settings;

        _settings.Changed += (_, updated) => Apply(updated);
    }

    public void Attach(FrameworkElement target)
    {
        ArgumentNullException.ThrowIfNull(target);

        if (!_targets.Any(reference => reference.TryGetTarget(out var existing) && ReferenceEquals(existing, target)))
        {
            _targets.Add(new WeakReference<FrameworkElement>(target));
        }

        Apply(_settings.Current);
    }

    public void Apply(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        ApplyTheme(settings.Theme);
        ApplyAccent(settings);
        ApplyMetrics(settings);
    }

    private void ApplyTheme(AppTheme theme)
    {
        var elementTheme = theme switch
        {
            AppTheme.Light => ElementTheme.Light,
            AppTheme.Dark => ElementTheme.Dark,
            _ => ResolveSystemTheme(),
        };

        _targets.RemoveAll(reference => !reference.TryGetTarget(out _));

        foreach (var reference in _targets)
        {
            if (reference.TryGetTarget(out var target))
            {
                target.RequestedTheme = elementTheme;
            }
        }
    }

    private static ElementTheme ResolveSystemTheme()
    {
        try
        {
            var uiSettings = new UISettings();
            var background = uiSettings.GetColorValue(UIColorType.Background);

            return background.R > 128 ? ElementTheme.Light : ElementTheme.Dark;
        }
        catch (Exception)
        {
            return ElementTheme.Default;
        }
    }

    private static void ApplyAccent(AppSettings settings)
    {
        if (Application.Current is not { Resources: { } resources })
        {
            return;
        }

        if (settings.Accent == AccentSource.Custom &&
            settings.AccentColor is { Length: 7 } hex &&
            hex[0] == '#')
        {
            try
            {
                var color = ParseHex(hex);
                resources["SystemAccentColor"] = color;
                resources["SystemAccentColorLight2"] = color;
                resources["SystemAccentColorDark1"] = color;
            }
            catch (FormatException)
            {
            }
        }
    }

    private static Windows.UI.Color ParseHex(string hex) => Windows.UI.Color.FromArgb(
        255,
        Convert.ToByte(hex.Substring(1, 2), 16),
        Convert.ToByte(hex.Substring(3, 2), 16),
        Convert.ToByte(hex.Substring(5, 2), 16));

    private static void ApplyMetrics(AppSettings settings)
    {
        if (Application.Current is not { Resources: { } resources })
        {
            return;
        }

        resources["GridHourHeight"] = (double)settings.HourHeight;
    }
}
