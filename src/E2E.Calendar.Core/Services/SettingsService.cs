using E2E.Calendar.Core.Abstractions;
using E2E.Calendar.Core.Models;

namespace E2E.Calendar.Core.Services;

/// <summary>
/// Loads and persists <see cref="AppSettings"/>. Owns the mapping between the
/// typed model and the raw key/value rows so every consumer reads consistent data.
/// </summary>
public sealed class SettingsService : ISettingsService, System.ComponentModel.INotifyPropertyChanged
{
    private const string ThemeKey = "appearance.theme";
    private const string AccentKey = "appearance.accent";
    private const string AccentColorKey = "appearance.accent_color";
    private const string HourHeightKey = "appearance.hour_height";
    private const string ClockFormatKey = "general.clock_format";
    private const string WeekStartKey = "general.week_start";
    private const string ShowWeekendsKey = "general.show_weekends";
    private const string ShowWeekNumbersKey = "general.show_week_numbers";
    private const string ShowSecondaryTimeZoneKey = "general.show_secondary_time_zone";
    private const string ShowBirthdaysKey = "calendars.show_birthdays";
    private const string SecondaryTimeZoneIdKey = "general.secondary_time_zone_id";
    private const string DensityKey = "appearance.density";
    private const string LastViewModeKey = "general.last_view_mode";
    private const string OnboardingCompleteKey = "app.onboarding_complete";

    private readonly ISettingsRepository _repository;

    private AppSettings _current = new();

    public SettingsService(ISettingsRepository repository)
    {
        ArgumentNullException.ThrowIfNull(repository);
        _repository = repository;
    }

    public event EventHandler<AppSettings>? Changed;
    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

    public AppSettings Current => _current;
    public AppTheme Theme => _current.Theme;
    public WeekStartDay WeekStart => _current.WeekStart;
    public ClockFormat Clock => _current.ClockFormat;
    public bool ShowWeekends => _current.ShowWeekends;
    public bool ShowWeekNumbersWeek => _current.ShowWeekNumbers;
    public bool ShowWeekNumbersMonth => _current.ShowWeekNumbers;
    public bool ShowSecondaryTimeZone => _current.ShowSecondaryTimeZone;
    public string? SecondaryTimeZoneId => _current.SecondaryTimeZoneId;
    public AppDensity Density => _current.Density;

    public async Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        var values = await _repository.GetAllAsync(cancellationToken);

        _current = FromValues(values);
        NotifyProperties();

        return _current;
    }

    public async Task UpdateAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        _current = settings;
        NotifyProperties();

        await _repository.SetAsync(ThemeKey, settings.Theme.ToString(), cancellationToken);
        await _repository.SetAsync(AccentKey, settings.Accent.ToString(), cancellationToken);
        await _repository.SetAsync(AccentColorKey, settings.AccentColor, cancellationToken);
        await _repository.SetAsync(HourHeightKey, settings.HourHeight.ToString(), cancellationToken);
        await _repository.SetAsync(ClockFormatKey, settings.ClockFormat.ToString(), cancellationToken);
        await _repository.SetAsync(WeekStartKey, settings.WeekStart.ToString(), cancellationToken);
        await _repository.SetAsync(ShowWeekendsKey, settings.ShowWeekends ? "1" : "0", cancellationToken);
        await _repository.SetAsync(ShowWeekNumbersKey, settings.ShowWeekNumbers ? "1" : "0", cancellationToken);
        await _repository.SetAsync(
            ShowSecondaryTimeZoneKey,
            settings.ShowSecondaryTimeZone ? "1" : "0",
            cancellationToken);
        await _repository.SetAsync(ShowBirthdaysKey, settings.ShowBirthdays ? "1" : "0", cancellationToken);
        await _repository.SetAsync(SecondaryTimeZoneIdKey, settings.SecondaryTimeZoneId, cancellationToken);
        await _repository.SetAsync(DensityKey, settings.Density.ToString(), cancellationToken);
        await _repository.SetAsync(LastViewModeKey, settings.LastViewMode, cancellationToken);
        await _repository.SetAsync(
            OnboardingCompleteKey,
            settings.OnboardingComplete ? "1" : "0",
            cancellationToken);

        Changed?.Invoke(this, _current);
    }

    private void NotifyProperties()
    {
        PropertyChanged?.Invoke(this, new(nameof(Current)));
        PropertyChanged?.Invoke(this, new(nameof(Theme)));
        PropertyChanged?.Invoke(this, new(nameof(WeekStart)));
        PropertyChanged?.Invoke(this, new(nameof(Clock)));
        PropertyChanged?.Invoke(this, new(nameof(ShowWeekends)));
        PropertyChanged?.Invoke(this, new(nameof(ShowWeekNumbersWeek)));
        PropertyChanged?.Invoke(this, new(nameof(ShowWeekNumbersMonth)));
        PropertyChanged?.Invoke(this, new(nameof(ShowSecondaryTimeZone)));
        PropertyChanged?.Invoke(this, new(nameof(SecondaryTimeZoneId)));
        PropertyChanged?.Invoke(this, new(nameof(Density)));
    }

    private static AppSettings FromValues(IReadOnlyDictionary<string, string?> values)
    {
        var defaults = new AppSettings();

        return new AppSettings
        {
            Theme = ParseEnum(values, ThemeKey, defaults.Theme),
            Accent = ParseEnum(values, AccentKey, defaults.Accent),
            AccentColor = Get(values, AccentColorKey),
            HourHeight = ParseInt(values, HourHeightKey, defaults.HourHeight, AppSettings.MinHourHeight, AppSettings.MaxHourHeight),
            ClockFormat = ParseEnum(values, ClockFormatKey, defaults.ClockFormat),
            WeekStart = ParseEnum(values, WeekStartKey, defaults.WeekStart),
            ShowWeekends = ParseBool(values, ShowWeekendsKey, defaults.ShowWeekends),
            ShowWeekNumbers = ParseBool(values, ShowWeekNumbersKey, defaults.ShowWeekNumbers),
            ShowSecondaryTimeZone = ParseBool(values, ShowSecondaryTimeZoneKey, defaults.ShowSecondaryTimeZone),
            ShowBirthdays = ParseBool(values, ShowBirthdaysKey, defaults.ShowBirthdays),
            SecondaryTimeZoneId = Get(values, SecondaryTimeZoneIdKey),
            Density = ParseEnum(values, DensityKey, defaults.Density),
            LastViewMode = Get(values, LastViewModeKey),
            OnboardingComplete = ParseBool(values, OnboardingCompleteKey, defaults.OnboardingComplete),
        };
    }

    private static string? Get(IReadOnlyDictionary<string, string?> values, string key) =>
        values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value : null;

    private static TEnum ParseEnum<TEnum>(IReadOnlyDictionary<string, string?> values, string key, TEnum fallback)
        where TEnum : struct, Enum =>
        Get(values, key) is { } raw && Enum.TryParse<TEnum>(raw, ignoreCase: true, out var parsed) ? parsed : fallback;

    private static bool ParseBool(IReadOnlyDictionary<string, string?> values, string key, bool fallback) =>
        Get(values, key) switch
        {
            "1" or "true" or "True" => true,
            "0" or "false" or "False" => false,
            _ => fallback,
        };

    private static int ParseInt(
        IReadOnlyDictionary<string, string?> values,
        string key,
        int fallback,
        int min,
        int max)
    {
        if (Get(values, key) is { } raw && int.TryParse(raw, out var parsed))
        {
            return Math.Clamp(parsed, min, max);
        }

        return fallback;
    }
}
