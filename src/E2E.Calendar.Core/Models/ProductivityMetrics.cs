namespace E2E.Calendar.Core.Models;

public sealed record ProductivityMetrics
{
    public required int CompletedEventCount { get; init; }

    public required TimeSpan CompletedDuration { get; init; }

    public required IReadOnlyDictionary<string, TimeSpan> DurationByCategory { get; init; }
}
