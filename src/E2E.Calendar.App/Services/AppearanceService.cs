using E2E.Calendar.App.Utilities;
using E2E.Calendar.Core.Models;
using E2E.Calendar.Core.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.UI.ViewManagement;

namespace E2E.Calendar.App.Services;

/// <summary>
/// Applies <see cref="AppSettings"/> to the running UI: theme, accent colour and
/// grid metrics. The theme is applied to each window root because
/// <see cref="Application.RequestedTheme"/> can only be set at startup in WinUI 3.
/// </summary>
public sealed class AppearanceService
{
    private readonly SettingsService _settings;
    private readonly List<WeakReference<FrameworkElement>> _targets = [];

    public AppearanceService(SettingsService settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        _settings = settings;

        _settings.Changed += (_, updated) => Apply(updated);
    }

    public void Attach(FrameworkElement target)
    {
        ArgumentNullException.ThrowIfNull(target);

        _targets.RemoveAll(reference =>
            !reference.TryGetTarget(out var existing) || ReferenceEquals(existing, target));

        _targets.Add(new WeakReference<FrameworkElement>(target));

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
        var requested = theme switch
        {
            AppTheme.Light => ElementTheme.Light,
            AppTheme.Dark => ElementTheme.Dark,
            _ => ResolveSystemTheme(),
        };

        CalendarColors.Theme = requested;

        foreach (var reference in _targets.ToArray())
        {
            if (reference.TryGetTarget(out var target))
            {
                target.RequestedTheme = requested;
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

        var palette = settings.Accent == AccentSource.Custom
            ? CalendarPalette.Normalize(settings.AccentColor)
            : CalendarPalette.Indigo;

        if (CalendarColors.Bar(null, palette) is not SolidColorBrush { Color: var color })
        {
            return;
        }

        resources["SystemAccentColor"] = color;
        resources["SystemAccentColorLight2"] = color;
        resources["SystemAccentColorDark1"] = color;
    }

    private static void ApplyMetrics(AppSettings settings)
    {
        if (Application.Current is not { Resources: { } resources })
        {
            return;
        }

        resources["GridHourHeight"] = (double)settings.HourHeight;
    }
}
