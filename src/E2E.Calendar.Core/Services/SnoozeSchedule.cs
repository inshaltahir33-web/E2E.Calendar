namespace E2E.Calendar.Core.Services;

public enum SnoozeChoice
{
    FiveMinutes,
    TenMinutes,
    ThirtyMinutes,
    OneHour,
    TomorrowAtNine,
}

/// <summary>
/// The snooze options offered by the reminder toast. Keys are the values carried in
/// the toast activation payload, so they stay stable independent of display text.
/// </summary>
public static class SnoozeSchedule
{
    /// <summary>Input id of the snooze menu in the toast; activation returns it in UserInput.</summary>
    public const string ToastInputId = "snooze";

    private const string TomorrowAtNineKey = "tomorrow-9";

    public static IReadOnlyList<SnoozeChoice> Choices { get; } = new[]
    {
        SnoozeChoice.FiveMinutes,
        SnoozeChoice.TenMinutes,
        SnoozeChoice.ThirtyMinutes,
        SnoozeChoice.OneHour,
        SnoozeChoice.TomorrowAtNine,
    };

    public static string ToKey(SnoozeChoice choice) => choice switch
    {
        SnoozeChoice.FiveMinutes => "5",
        SnoozeChoice.TenMinutes => "10",
        SnoozeChoice.ThirtyMinutes => "30",
        SnoozeChoice.OneHour => "60",
        SnoozeChoice.TomorrowAtNine => TomorrowAtNineKey,
        _ => throw new ArgumentOutOfRangeException(nameof(choice)),
    };

    public static string ToLabel(SnoozeChoice choice) => choice switch
    {
        SnoozeChoice.FiveMinutes => "5 min",
        SnoozeChoice.TenMinutes => "10 min",
        SnoozeChoice.ThirtyMinutes => "30 min",
        SnoozeChoice.OneHour => "1 hour",
        SnoozeChoice.TomorrowAtNine => "Tomorrow 9 AM",
        _ => throw new ArgumentOutOfRangeException(nameof(choice)),
    };

    /// <summary>Minutes to snooze, or null for a fixed wall-clock time.</summary>
    public static int? Minutes(SnoozeChoice choice) => choice switch
    {
        SnoozeChoice.FiveMinutes => 5,
        SnoozeChoice.TenMinutes => 10,
        SnoozeChoice.ThirtyMinutes => 30,
        SnoozeChoice.OneHour => 60,
        _ => null,
    };

    public static bool TryParse(string? key, out SnoozeChoice choice)
    {
        switch (key)
        {
            case "5":
                choice = SnoozeChoice.FiveMinutes;
                return true;
            case "10":
                choice = SnoozeChoice.TenMinutes;
                return true;
            case "30":
                choice = SnoozeChoice.ThirtyMinutes;
                return true;
            case "60":
                choice = SnoozeChoice.OneHour;
                return true;
            case TomorrowAtNineKey:
                choice = SnoozeChoice.TomorrowAtNine;
                return true;
            default:
                choice = default;
                return false;
        }
    }

    public static DateTimeOffset Resolve(SnoozeChoice choice, DateTimeOffset nowUtc, TimeZoneInfo timeZone)
    {
        ArgumentNullException.ThrowIfNull(timeZone);

        if (Minutes(choice) is { } minutes)
        {
            return nowUtc.AddMinutes(minutes);
        }

        var tomorrowLocal = timeZone.ToLocal(nowUtc).Date.AddDays(1).AddHours(9);
        return timeZone.ToUtc(tomorrowLocal);
    }
}
