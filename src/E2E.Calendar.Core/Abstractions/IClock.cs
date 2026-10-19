namespace E2E.Calendar.Core.Abstractions;

public interface IClock
{
    DateTimeOffset UtcNow { get; }

    TimeZoneInfo LocalTimeZone { get; }
}
