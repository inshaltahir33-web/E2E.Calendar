using System.Globalization;

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

    public static DateTime ToLocal(this TimeZoneInfo timeZone, DateTimeOffset utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(utc.UtcDateTime, timeZone);

    public static DateTimeOffset ToUtc(this TimeZoneInfo timeZone, DateTime local)
    {
        ArgumentNullException.ThrowIfNull(timeZone);

        var unspecified = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);

        if (timeZone.IsInvalidTime(unspecified))
        {
            var offsetBefore = timeZone.GetUtcOffset(unspecified.AddDays(-1));
            var offsetAfter = timeZone.GetUtcOffset(unspecified.AddDays(1));

            unspecified = unspecified.Add(offsetAfter - offsetBefore);
        }

        var offset = timeZone.GetUtcOffset(unspecified);

        return new DateTimeOffset(unspecified, offset).ToUniversalTime();
    }

    public static DateOnly ToDateOnly(this DateTimeOffset value) =>
        DateOnly.FromDateTime(value.Date);
}
