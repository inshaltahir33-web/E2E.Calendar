using CommunityToolkit.Mvvm.ComponentModel;
using E2E.Calendar.Core.Models;
using Microsoft.UI.Xaml;

namespace E2E.Calendar.App.ViewModels;

public sealed partial class MonthDay : ObservableObject
{
    [ObservableProperty]
    private bool _isSelected;

    public required DateOnly Date { get; init; }

    public required int DayNumber { get; init; }

    public required bool IsToday { get; init; }

    public required bool IsCurrentMonth { get; init; }

    public required bool IsWeekend { get; init; }

    public required IReadOnlyList<MonthChip> Chips { get; init; }

    public required IReadOnlyList<CalendarItem> AllItems { get; init; }

    public required int MoreCount { get; init; }

    public required int EventCount { get; init; }

    /// <summary>Number of top lines reserved by the week's banner bars in this cell.</summary>
    public required int LineOffset { get; init; }

    public string MoreLabel => $"+{MoreCount} more";

    public Visibility SelectionVisibility => IsSelected ? Visibility.Visible : Visibility.Collapsed;

    partial void OnIsSelectedChanged(bool value) => OnPropertyChanged(nameof(SelectionVisibility));
}

/// <summary>A banner bar inside one week row; columns are zero-based day slots.</summary>
public sealed record MonthBanner(
    string Id,
    string Title,
    string? Color,
    int StartColumn,
    int EndColumn,
    int Lane,
    bool ContinuesFromPrev,
    bool ContinuesToNext);

public sealed record MonthWeek(int Row, IReadOnlyList<MonthBanner> Banners);
