namespace E2E.Calendar.Core.Models;

public sealed record MeetingSlot
{
    public required DateTimeOffset StartUtc { get; init; }

    public required DateTimeOffset EndUtc { get; init; }

    public TimeSpan Duration => EndUtc - StartUtc;
}
