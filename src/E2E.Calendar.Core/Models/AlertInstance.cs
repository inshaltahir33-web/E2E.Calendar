namespace E2E.Calendar.Core.Models;

public sealed record AlertInstance
{
    public required string Id { get; init; }

    public required string AlertId { get; init; }

    public required OwnerType OwnerType { get; init; }

    public required string OwnerId { get; init; }

    public required DateTimeOffset OccurrenceUtc { get; init; }

    public required DateTimeOffset FireAtUtc { get; init; }

    public AlertState State { get; init; } = AlertState.Pending;

    public DateTimeOffset? SnoozedUntilUtc { get; init; }

    public DateTimeOffset? FiredUtc { get; init; }

    public int RepeatCount { get; init; }
}
