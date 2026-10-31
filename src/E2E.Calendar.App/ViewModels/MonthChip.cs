using E2E.Calendar.Core.Models;

namespace E2E.Calendar.App.ViewModels;

public sealed class MonthChip
{
    public required string Id { get; init; }

    public required string Title { get; init; }

    public string? Color { get; init; }

    public string? CalendarName { get; init; }

    public string? Location { get; init; }

    public DateTimeOffset StartUtc { get; init; }

    public DateTimeOffset EndUtc { get; init; }

    public bool AllDay { get; init; }

    public bool HasTime { get; init; } = true;

    public bool IsReminder { get; init; }

    public bool IsTask { get; init; }
}
