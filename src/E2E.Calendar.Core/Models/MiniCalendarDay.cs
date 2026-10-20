namespace E2E.Calendar.Core.Models;

public sealed record MiniCalendarDay
{
    public required DateOnly Date { get; init; }

    public required bool IsCurrentMonth { get; init; }

    public required bool IsToday { get; init; }

    public required bool IsSelected { get; init; }

    public required bool IsInSelectedWeek { get; init; }

    public required int EventCount { get; init; }
}

public sealed record MiniCalendarWeek(IReadOnlyList<MiniCalendarDay> Days);

public sealed record MiniCalendarGrid(
    IReadOnlyList<MiniCalendarWeek> Weeks,
    DateOnly FirstVisibleDay,
    DateOnly LastVisibleDay);
