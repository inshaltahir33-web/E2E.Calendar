namespace E2E.Calendar.Core.Models;

public sealed record TaskItem
{
    public required string Id { get; init; }

    public string? ListId { get; init; }

    public required string Title { get; init; }

    public string? Notes { get; init; }

    public DateTimeOffset? DueUtc { get; init; }

    public bool HasTime { get; init; }

    public Priority Priority { get; init; } = Priority.Normal;

    public string? RRule { get; init; }

    public DateTimeOffset? DoneUtc { get; init; }

    public string? ParentId { get; init; }

    public string? LinkedEventId { get; init; }

    public bool Dirty { get; init; }

    public bool Deleted { get; init; }
}
