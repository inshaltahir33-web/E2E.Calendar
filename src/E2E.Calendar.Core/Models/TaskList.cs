namespace E2E.Calendar.Core.Models;

public sealed record TaskList
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public string? Color { get; init; }

    public string? RemoteId { get; init; }
}
