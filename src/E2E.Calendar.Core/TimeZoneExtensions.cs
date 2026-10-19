namespace E2E.Calendar.Core;

public static class TimeZoneExtensions
{
    public static string ToIanaId(this TimeZoneInfo timeZone)
    {
        ArgumentNullException.ThrowIfNull(timeZone);

        if (TimeZoneInfo.TryConvertWindowsIdToIanaId(timeZone.Id, out var ianaId))
        {
            return ianaId;
        }

        return timeZone.Id;
    }

    public static TimeZoneInfo ToTimeZoneInfo(this string ianaId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ianaId);

        return TimeZoneInfo.FindSystemTimeZoneById(ianaId);
    }

    public static bool TryToTimeZoneInfo(this string? ianaId, out TimeZoneInfo timeZone)
    {
        timeZone = TimeZoneInfo.Utc;

        if (string.IsNullOrWhiteSpace(ianaId))
        {
            return false;
        }

        try
        {
            timeZone = TimeZoneInfo.FindSystemTimeZoneById(ianaId);
            return true;
        }
        catch (TimeZoneNotFoundException)
        {
            return false;
        }
        catch (InvalidTimeZoneException)
        {
            return false;
        }
    }
}
