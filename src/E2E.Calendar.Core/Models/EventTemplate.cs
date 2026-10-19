namespace E2E.Calendar.Core.Models;

public sealed record EventTemplate
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public required string Title { get; init; }

    public int DurationMinutes { get; init; } = 60;

    public int LeadTimeMinutes { get; init; } = 15;

    public string? Color { get; init; }
}
