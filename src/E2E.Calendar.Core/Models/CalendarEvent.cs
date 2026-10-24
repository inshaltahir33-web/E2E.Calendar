namespace E2E.Calendar.Core.Models;

public sealed record CalendarEvent
{
    public required string Id { get; init; }

    public required string CalendarId { get; init; }

    public string? RemoteId { get; init; }

    public string? ETag { get; init; }

    public required string Title { get; init; }

    public string? Notes { get; init; }

    public string? Location { get; init; }

    public string? Url { get; init; }

    public required DateTimeOffset StartUtc { get; init; }

    public required DateTimeOffset EndUtc { get; init; }

    public bool AllDay { get; init; }

    public required string TimeZoneId { get; init; }

    public string? RRule { get; init; }

    public string? ExDates { get; init; }

    public string? RecurrenceMasterId { get; init; }

    public DateTimeOffset? RecurrenceIdUtc { get; init; }

    public string? Color { get; init; }

    public string? MeetingUrl { get; init; }

    public string? Attendees { get; init; }

    public string? Tags { get; init; }

    public string? Category { get; init; }

    public int? TravelMinutes { get; init; }

    public required DateTimeOffset CreatedUtc { get; init; }

    public required DateTimeOffset UpdatedUtc { get; init; }

    public bool Deleted { get; init; }

    public bool Dirty { get; init; }
}
