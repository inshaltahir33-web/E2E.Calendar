namespace E2E.Calendar.Core.Models;

public sealed record Alert
{
    public required string Id { get; init; }

    public required OwnerType OwnerType { get; init; }

    public required string OwnerId { get; init; }

    public int? TriggerOffsetMinutes { get; init; }

    public DateTimeOffset? TriggerAtUtc { get; init; }

    public Priority Priority { get; init; } = Priority.Normal;

    public bool Persistent { get; init; }

    public int? RepeatEveryMinutes { get; init; }

    public string? Sound { get; init; }
}
