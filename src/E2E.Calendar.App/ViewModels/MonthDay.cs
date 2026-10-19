using E2E.Calendar.Core.Models;
using Microsoft.UI.Xaml;

namespace E2E.Calendar.App.ViewModels;

public sealed class MonthDay
{
    public required DateOnly Date { get; init; }

    public required int DayNumber { get; init; }

    public required bool IsToday { get; init; }

    public required bool IsCurrentMonth { get; init; }

    public required IReadOnlyList<MonthChip> Chips { get; init; }

    public required IReadOnlyList<CalendarItem> AllItems { get; init; }

    public required int MoreCount { get; init; }

    public required int EventCount { get; init; }

    public string MoreLabel => $"+{MoreCount} more";

    public Visibility MoreVisibility => MoreCount > 0 ? Visibility.Visible : Visibility.Collapsed;

    public double Heat => EventCount switch
    {
        0 => 0,
        1 => 0.25,
        2 => 0.5,
        3 => 0.75,
        _ => 1,
    };

    public double Heat1 => EventCount == 0 ? 0 : 0.35;

    public double Heat2 => EventCount <= 1 ? 0 : 0.65;

    public double Heat3 => EventCount <= 3 ? 0 : 0.95;
}
