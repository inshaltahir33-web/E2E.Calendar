namespace E2E.Calendar.Core.Models;

public sealed record EventTemplate
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public required string Title { get; init; }

    public int DurationMinutes { get; init; } = 60;

    public string? CalendarId { get; init; }

    public string? Location { get; init; }

    public string? Notes { get; init; }

    public IReadOnlyList<int> AlertOffsetsMinutes { get; init; } = [];

    public DateTimeOffset CreatedUtc { get; init; }
}
