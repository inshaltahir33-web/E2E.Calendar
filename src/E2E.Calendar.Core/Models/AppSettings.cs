namespace E2E.Calendar.Core.Models;

/// <summary>
/// The app colour theme preference.
/// </summary>
public enum AppTheme
{
    System,
    Light,
    Dark,
}

/// <summary>
/// How the accent colour is chosen.
/// </summary>
public enum AccentSource
{
    System,
    Calendar,
    Custom,
}

/// <summary>
/// User-facing preferences persisted in the <c>settings</c> table.
/// </summary>
public sealed record AppSettings
{
    public const int MinHourHeight = 40;

    public const int MaxHourHeight = 96;

    public const int DefaultHourHeight = 56;

    public AppTheme Theme { get; init; } = AppTheme.System;

    public AccentSource Accent { get; init; } = AccentSource.System;

    public string? AccentColor { get; init; }

    public int HourHeight { get; init; } = DefaultHourHeight;

    public ClockFormat ClockFormat { get; init; } = ClockFormat.TwelveHour;

    public WeekStartDay WeekStart { get; init; } = WeekStartDay.Monday;

    public bool ShowWeekends { get; init; } = true;

    public bool ShowWeekNumbers { get; init; }

    public bool ShowSecondaryTimeZone { get; init; }

    public bool ShowBirthdays { get; init; }

    public string? SecondaryTimeZoneId { get; init; }

    public int DefaultSnoozeMinutes { get; init; } = 10;

    public bool DailyDigestEnabled { get; init; }

    public TimeSpan DailyDigestTime { get; init; } = new(8, 0, 0);

    public bool CriticalBreaksThroughDoNotDisturb { get; init; } = true;

    public int PersistentRepeatMinutes { get; init; } = 10;

    public string? LastViewMode { get; init; }

    public bool OnboardingComplete { get; init; }

    public bool StartMinimizedToTray { get; init; }

    public int? WindowLeft { get; init; }

    public int? WindowTop { get; init; }

    public int? WindowWidth { get; init; }

    public int? WindowHeight { get; init; }
}
