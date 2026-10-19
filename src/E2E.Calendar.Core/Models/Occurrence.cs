namespace E2E.Calendar.Core.Models;

public sealed record Occurrence
{
    public required string EventId { get; init; }

    public required DateTimeOffset StartUtc { get; init; }

    public required DateTimeOffset EndUtc { get; init; }

    public bool IsException { get; init; }
}
