using E2E.Calendar.Core.Models;

namespace E2E.Calendar.Core.QuickAdd;

public enum ParsedItemKind
{
    Event = 0,
    Reminder = 1,
}

/// <summary>
/// Result of parsing a natural-language quick-add phrase. Times are resolved to
/// UTC using the caller's clock/time zone; the local date and clock values are
/// kept for display and editing.
/// </summary>
public sealed record ParsedItem
{
    public ParsedItemKind Kind { get; init; } = ParsedItemKind.Event;

    public required string Title { get; init; }

    public required DateOnly Date { get; init; }

    public TimeOnly? StartTime { get; init; }

    public TimeOnly? EndTime { get; init; }

    public required DateTimeOffset StartUtc { get; init; }

    public required DateTimeOffset EndUtc { get; init; }

    public bool AllDay { get; init; }

    /// <summary>True when the phrase named a day (explicit date, relative offset or weekday).</summary>
    public bool HasDate { get; init; }

    public string? Location { get; init; }

    public string? CalendarHint { get; init; }

    public IReadOnlyList<int> AlertOffsetsMinutes { get; init; } = Array.Empty<int>();

    public string? RRule { get; init; }

    public Priority Priority { get; init; } = Priority.Normal;

    public double Confidence { get; init; } = 0.5;

    public bool HasTime => StartTime is not null;

    public bool IsRecurring => !string.IsNullOrWhiteSpace(RRule);
}
