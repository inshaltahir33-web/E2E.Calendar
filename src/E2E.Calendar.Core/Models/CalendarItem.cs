namespace E2E.Calendar.Core.Models;

public sealed record CalendarItem
{
    public required string Id { get; init; }

    public required string Title { get; init; }

    public required DateTimeOffset StartUtc { get; init; }

    public required DateTimeOffset EndUtc { get; init; }

    public CalendarItemKind Kind { get; init; } = CalendarItemKind.Event;

    public string? CalendarId { get; init; }

    public string? Color { get; init; }

    public string? Location { get; init; }

    public double? Latitude { get; init; }

    public double? Longitude { get; init; }

    public string? CalendarName { get; init; }

    public bool AllDay { get; init; }

    public bool HasTime { get; init; } = true;

    public bool IsDone { get; init; }

    public bool IsException { get; init; }

    public bool IsRecurring { get; init; }

    public bool IsTickable => Kind is CalendarItemKind.Reminder or CalendarItemKind.Task;

    public string TimeLabel => HasTime
        ? StartUtc.ToLocalTime().ToString("t", System.Globalization.CultureInfo.CurrentCulture)
        : string.Empty;

    public TimeSpan Duration => EndUtc - StartUtc;
}
