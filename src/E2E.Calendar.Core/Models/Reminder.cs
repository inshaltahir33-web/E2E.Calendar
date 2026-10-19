namespace E2E.Calendar.Core.Models;

public sealed record Reminder
{
    public required string Id { get; init; }

    public required string Title { get; init; }

    public string? Notes { get; init; }

    public DateTimeOffset? DueUtc { get; init; }

    public required string TimeZoneId { get; init; }

    public string? RRule { get; init; }

    public Priority Priority { get; init; } = Priority.Normal;

    public bool Flagged { get; init; }

    public DateTimeOffset? DoneUtc { get; init; }

    public string? ListId { get; init; }

    public required DateTimeOffset CreatedUtc { get; init; }

    public required DateTimeOffset UpdatedUtc { get; init; }

    public bool Deleted { get; init; }

    public bool Dirty { get; init; }
}
