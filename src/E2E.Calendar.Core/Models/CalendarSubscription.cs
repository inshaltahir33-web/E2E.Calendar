namespace E2E.Calendar.Core.Models;

/// <summary>
/// A read-only calendar backed by a remote iCalendar (.ics) feed.
/// </summary>
public sealed record CalendarSubscription
{
    public required string CalendarId { get; init; }

    public required string Url { get; init; }

    /// <summary>How often the feed is fetched, in hours (1-24).</summary>
    public int RefreshHours { get; init; } = 6;

    public string? ETag { get; init; }

    public DateTimeOffset? LastModifiedUtc { get; init; }

    public DateTimeOffset? LastRefreshUtc { get; init; }
}
