using System.Globalization;

namespace E2E.Calendar.Data.Types;

public static class SqliteTime
{
    public const string Format = "yyyy-MM-ddTHH:mm:ss.fffffffZ";

    public static string ToUtcText(DateTimeOffset value) =>
        value.ToUniversalTime().ToString(Format, CultureInfo.InvariantCulture);

    public static DateTimeOffset Parse(string? value) =>
        DateTimeOffset.Parse(
            value ?? throw new InvalidOperationException("A UTC timestamp column cannot be null."),
            CultureInfo.InvariantCulture,
            DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal);
}
