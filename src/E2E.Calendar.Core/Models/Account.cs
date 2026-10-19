namespace E2E.Calendar.Core.Models;

public sealed record Account
{
    public required string Id { get; init; }

    public required Provider Provider { get; init; }

    public required string DisplayName { get; init; }

    public string? Email { get; init; }

    public string? SyncToken { get; init; }

    public DateTimeOffset? LastSyncUtc { get; init; }
}
