namespace E2E.Calendar.Core.Models;

public sealed record EventSearchCriteria
{
    public string? Text { get; init; }

    public DateTimeOffset? FromUtc { get; init; }

    public DateTimeOffset? ToUtc { get; init; }

    public IReadOnlyList<string> Tags { get; init; } = [];

    public string? Category { get; init; }

    public string? CalendarId { get; init; }
}
