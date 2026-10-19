namespace E2E.Calendar.Core.Models;

public sealed record NotificationAction(string Id, string Label);

public sealed record NotificationRequest
{
    public required string Tag { get; init; }

    public required string Title { get; init; }

    public string? Body { get; init; }

    public required DateTimeOffset FireAtUtc { get; init; }

    public bool Critical { get; init; }

    public IReadOnlyList<NotificationAction> Actions { get; init; } = Array.Empty<NotificationAction>();
}
