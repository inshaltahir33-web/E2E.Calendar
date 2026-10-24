namespace E2E.Calendar.Core.Models;

public sealed record EventAttachment
{
    public required string Id { get; init; }

    public required string EventId { get; init; }

    public required string FilePath { get; init; }

    public required string DisplayName { get; init; }

    public string? ContentType { get; init; }

    public long? SizeBytes { get; init; }

    public required DateTimeOffset AddedUtc { get; init; }
}
