using System.Globalization;
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
    private const string ShowRemindersAndTasksKey = "general.show_reminders_and_tasks";
    private const string ShowSecondaryTimeZoneKey = "general.show_secondary_time_zone";
    private const string ShowBirthdaysKey = "calendars.show_birthdays";
    private const string SecondaryTimeZoneIdKey = "general.secondary_time_zone_id";
    private const string DefaultSnoozeMinutesKey = "notifications.default_snooze_minutes";
    private const string NotificationsEnabledKey = "notifications.enabled";
    private const string DailyDigestEnabledKey = "notifications.daily_digest_enabled";
    private const string DailyDigestTimeKey = "notifications.daily_digest_time";
    private const string CriticalBreaksThroughDoNotDisturbKey = "notifications.critical_break_through_dnd";
    private const string PersistentRepeatMinutesKey = "notifications.persistent_repeat_minutes";
    private const string LastViewModeKey = "general.last_view_mode";
    private const string TimeOfDayFormat = @"hh\:mm";
    private const string OnboardingCompleteKey = "app.onboarding_complete";
    private const string StartMinimizedToTrayKey = "app.start_minimized_to_tray";
    private const string WindowLeftKey = "window.left";
    private const string WindowTopKey = "window.top";
    private const string WindowWidthKey = "window.width";
    private const string WindowHeightKey = "window.height";

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
    public bool ShowRemindersAndTasks => _current.ShowRemindersAndTasks;
    public bool ShowSecondaryTimeZone => _current.ShowSecondaryTimeZone;
    public string? SecondaryTimeZoneId => _current.SecondaryTimeZoneId;
    public bool StartMinimizedToTray => _current.StartMinimizedToTray;

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
            ShowRemindersAndTasksKey,
            settings.ShowRemindersAndTasks ? "1" : "0",
            cancellationToken);
        await _repository.SetAsync(
            ShowSecondaryTimeZoneKey,
            settings.ShowSecondaryTimeZone ? "1" : "0",
            cancellationToken);
        await _repository.SetAsync(ShowBirthdaysKey, settings.ShowBirthdays ? "1" : "0", cancellationToken);
        await _repository.SetAsync(SecondaryTimeZoneIdKey, settings.SecondaryTimeZoneId, cancellationToken);
        await _repository.SetAsync(DefaultSnoozeMinutesKey, settings.DefaultSnoozeMinutes.ToString(), cancellationToken);
        await _repository.SetAsync(
            NotificationsEnabledKey,
            settings.NotificationsEnabled ? "1" : "0",
            cancellationToken);
        await _repository.SetAsync(
            DailyDigestEnabledKey,
            settings.DailyDigestEnabled ? "1" : "0",
            cancellationToken);
        await _repository.SetAsync(
            DailyDigestTimeKey,
            settings.DailyDigestTime.ToString(TimeOfDayFormat, CultureInfo.InvariantCulture),
            cancellationToken);
        await _repository.SetAsync(
            CriticalBreaksThroughDoNotDisturbKey,
            settings.CriticalBreaksThroughDoNotDisturb ? "1" : "0",
            cancellationToken);
        await _repository.SetAsync(
            PersistentRepeatMinutesKey,
            settings.PersistentRepeatMinutes.ToString(),
            cancellationToken);
        await _repository.SetAsync(LastViewModeKey, settings.LastViewMode, cancellationToken);
        await _repository.SetAsync(
            OnboardingCompleteKey,
            settings.OnboardingComplete ? "1" : "0",
            cancellationToken);
        await _repository.SetAsync(
            StartMinimizedToTrayKey,
            settings.StartMinimizedToTray ? "1" : "0",
            cancellationToken);
        await _repository.SetAsync(WindowLeftKey, settings.WindowLeft?.ToString(), cancellationToken);
        await _repository.SetAsync(WindowTopKey, settings.WindowTop?.ToString(), cancellationToken);
        await _repository.SetAsync(WindowWidthKey, settings.WindowWidth?.ToString(), cancellationToken);
        await _repository.SetAsync(WindowHeightKey, settings.WindowHeight?.ToString(), cancellationToken);

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
        PropertyChanged?.Invoke(this, new(nameof(ShowRemindersAndTasks)));
        PropertyChanged?.Invoke(this, new(nameof(ShowSecondaryTimeZone)));
        PropertyChanged?.Invoke(this, new(nameof(SecondaryTimeZoneId)));
        PropertyChanged?.Invoke(this, new(nameof(StartMinimizedToTray)));
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
            ShowRemindersAndTasks = ParseBool(
                values,
                ShowRemindersAndTasksKey,
                defaults.ShowRemindersAndTasks),
            ShowSecondaryTimeZone = ParseBool(values, ShowSecondaryTimeZoneKey, defaults.ShowSecondaryTimeZone),
            ShowBirthdays = ParseBool(values, ShowBirthdaysKey, defaults.ShowBirthdays),
            SecondaryTimeZoneId = Get(values, SecondaryTimeZoneIdKey),
            DefaultSnoozeMinutes = ParseInt(
                values,
                DefaultSnoozeMinutesKey,
                defaults.DefaultSnoozeMinutes,
                1,
                1440),
            NotificationsEnabled = ParseBool(values, NotificationsEnabledKey, defaults.NotificationsEnabled),
            DailyDigestEnabled = ParseBool(values, DailyDigestEnabledKey, defaults.DailyDigestEnabled),
            DailyDigestTime = ParseTimeOfDay(values, DailyDigestTimeKey, defaults.DailyDigestTime),
            CriticalBreaksThroughDoNotDisturb = ParseBool(
                values,
                CriticalBreaksThroughDoNotDisturbKey,
                defaults.CriticalBreaksThroughDoNotDisturb),
            PersistentRepeatMinutes = ParseInt(
                values,
                PersistentRepeatMinutesKey,
                defaults.PersistentRepeatMinutes,
                1,
                1440),
            LastViewMode = Get(values, LastViewModeKey),
            OnboardingComplete = ParseBool(values, OnboardingCompleteKey, defaults.OnboardingComplete),
            StartMinimizedToTray = ParseBool(values, StartMinimizedToTrayKey, defaults.StartMinimizedToTray),
            WindowLeft = ParseNullableInt(values, WindowLeftKey),
            WindowTop = ParseNullableInt(values, WindowTopKey),
            WindowWidth = ParseNullableInt(values, WindowWidthKey),
            WindowHeight = ParseNullableInt(values, WindowHeightKey),
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

    private static TimeSpan ParseTimeOfDay(
        IReadOnlyDictionary<string, string?> values,
        string key,
        TimeSpan fallback)
    {
        if (Get(values, key) is { } raw &&
            TimeSpan.TryParseExact(raw, TimeOfDayFormat, CultureInfo.InvariantCulture, out var parsed) &&
            parsed >= TimeSpan.Zero && parsed < TimeSpan.FromDays(1))
        {
            return parsed;
        }

        return fallback;
    }

    private static int? ParseNullableInt(IReadOnlyDictionary<string, string?> values, string key)
    {
        if (Get(values, key) is { } raw && int.TryParse(raw, out var parsed))
        {
            return parsed;
        }

        return null;
    }

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
