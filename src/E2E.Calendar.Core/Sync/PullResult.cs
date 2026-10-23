using E2E.Calendar.Core.Models;

namespace E2E.Calendar.Core.Sync;

public sealed record PullResult
{
    public IReadOnlyList<CalendarEvent> Events { get; init; } = Array.Empty<CalendarEvent>();

    public string? NextSyncToken { get; init; }

    public bool ResetRequired { get; init; }
}
