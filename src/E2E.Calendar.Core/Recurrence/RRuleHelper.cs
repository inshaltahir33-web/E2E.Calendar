using System.Globalization;

namespace E2E.Calendar.Core.Recurrence;

public static class RRuleHelper
{
    public const string UntilParameter = "UNTIL";

    public static string? GetUntil(string? rRule)
    {
        if (string.IsNullOrWhiteSpace(rRule))
        {
            return null;
        }

        foreach (var part in rRule.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = part.IndexOf('=', StringComparison.Ordinal);

            if (separator <= 0)
            {
                continue;
            }

            if (!part.AsSpan(0, separator).Equals(UntilParameter, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return part[(separator + 1)..].Trim();
        }

        return null;
    }

    public static bool TryGetUntilUtc(string? rRule, out DateTimeOffset untilUtc)
    {
        untilUtc = default;

        var value = GetUntil(rRule);

        if (value is null || value.Length < 9 || !value.EndsWith('Z'))
        {
            return false;
        }

        return TryParseIcsDateTime(value, out untilUtc);
    }

    public static string SetUntil(string? rRule, DateTime untilLocal)
    {
        var value = untilLocal.ToString("yyyyMMdd'T'HHmmss", CultureInfo.InvariantCulture);
        var parts = new List<string>();

        foreach (var part in (rRule ?? string.Empty).Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = part.IndexOf('=', StringComparison.Ordinal);

            if (separator > 0 && part.AsSpan(0, separator).Equals(UntilParameter, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            parts.Add(part);
        }

        parts.Add($"{UntilParameter}={value}");

        return string.Join(';', parts);
    }

    public static bool TryParseIcsDateTime(string value, out DateTimeOffset result)
    {
        result = default;

        var text = value.Trim();

        if (text.EndsWith('Z'))
        {
            text = text[..^1];
        }

        if (text.Length != 15 || text[8] != 'T')
        {
            return false;
        }

        if (!int.TryParse(text.AsSpan(0, 4), NumberStyles.None, CultureInfo.InvariantCulture, out var year) ||
            !int.TryParse(text.AsSpan(4, 2), NumberStyles.None, CultureInfo.InvariantCulture, out var month) ||
            !int.TryParse(text.AsSpan(6, 2), NumberStyles.None, CultureInfo.InvariantCulture, out var day) ||
            !int.TryParse(text.AsSpan(9, 2), NumberStyles.None, CultureInfo.InvariantCulture, out var hour) ||
            !int.TryParse(text.AsSpan(11, 2), NumberStyles.None, CultureInfo.InvariantCulture, out var minute) ||
            !int.TryParse(text.AsSpan(13, 2), NumberStyles.None, CultureInfo.InvariantCulture, out var second))
        {
            return false;
        }

        try
        {
            result = new DateTimeOffset(year, month, day, hour, minute, second, TimeSpan.Zero);
            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }
    }

    public static string FormatUntilLocal(DateTime untilLocal) =>
        untilLocal.ToString("yyyyMMdd'T'HHmmss", CultureInfo.InvariantCulture);
}
