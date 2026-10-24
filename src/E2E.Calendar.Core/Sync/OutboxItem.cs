namespace E2E.Calendar.Core.Sync;

public enum OutboxOp
{
    Upsert = 0,
    Delete = 1,
}

public static class OutboxEntities
{
    public const string Event = "event";
    public const string Reminder = "reminder";
    public const string Task = "task";
}

public sealed record OutboxItem
{
    public long Id { get; init; }
    public required string Entity { get; init; }

    public required string EntityId { get; init; }

    public required OutboxOp Op { get; init; }

    public string? Payload { get; init; }

    public int Attempts { get; init; }

    public DateTimeOffset? NextTryUtc { get; init; }
}
