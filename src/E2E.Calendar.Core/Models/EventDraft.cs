namespace E2E.Calendar.Core.Models;

public sealed record EventDraft
{
    public required string CalendarId { get; init; }

    public required string Title { get; init; }

    public string? Notes { get; init; }

    public string? Location { get; init; }

    public double? Latitude { get; init; }

    public double? Longitude { get; init; }

    public string? Url { get; init; }

    public required DateTimeOffset StartUtc { get; init; }

    public required DateTimeOffset EndUtc { get; init; }

    public bool AllDay { get; init; }

    public required string TimeZoneId { get; init; }

    public string? RRule { get; init; }

    public string? ExDates { get; init; }

    public string? Color { get; init; }

    public string? MeetingUrl { get; init; }

    public int? TravelMinutes { get; init; }
}
