namespace E2E.Calendar.Core.Models;

public sealed record Calendar
{
    public required string Id { get; init; }

    public string? AccountId { get; init; }

    public string? RemoteId { get; init; }

    public required string Name { get; init; }

    public string? Color { get; init; }

    public bool Visible { get; init; } = true;

    public bool ReadOnly { get; init; }

    public bool IsLocal { get; init; }

    public string? SyncToken { get; init; }
}
