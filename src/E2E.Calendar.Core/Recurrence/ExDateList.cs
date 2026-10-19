using System.Globalization;

namespace E2E.Calendar.Core.Recurrence;

public static class ExDateList
{
    public const string DateFormat = "yyyy-MM-dd'T'HH:mm:ss.fffffffZ";

    public static IReadOnlyList<DateTimeOffset> Parse(string? exDates)
    {
        if (string.IsNullOrWhiteSpace(exDates))
        {
            return Array.Empty<DateTimeOffset>();
        }

        var result = new List<DateTimeOffset>();

        foreach (var part in exDates.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (TryParse(part, out var value))
            {
                result.Add(value);
            }
        }

        return result;
    }

    public static string Format(IEnumerable<DateTimeOffset> exDates) =>
        string.Join(',', exDates.Select(Format));

    public static string Format(DateTimeOffset exDate) =>
        exDate.ToUniversalTime().ToString(DateFormat, CultureInfo.InvariantCulture);

    public static bool TryParse(string value, out DateTimeOffset result)
    {
        result = default;

        var text = value.Trim();

        if (text.Length == 0)
        {
            return false;
        }

        if (DateTimeOffset.TryParse(
                text,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
                out result))
        {
            return true;
        }

        return RRuleHelper.TryParseIcsDateTime(text, out result);
    }
}
