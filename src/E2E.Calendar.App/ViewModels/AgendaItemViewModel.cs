using CommunityToolkit.Mvvm.ComponentModel;
using E2E.Calendar.Core.Models;
using Microsoft.UI.Xaml;

namespace E2E.Calendar.App.ViewModels;

public sealed partial class AgendaItemViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _isCompleted;

    public required string Id { get; init; }

    public required string Title { get; init; }

    public string? Location { get; init; }

    public string? CalendarName { get; init; }

    public string? Color { get; init; }

    public string TimeLabel { get; init; } = string.Empty;

    public bool HasTime { get; init; }

    public bool IsReminder { get; init; }

    public bool IsTask { get; init; }

    public bool IsDone { get; init; }

    public bool IsTickable => IsReminder || IsTask;

    public Visibility CheckboxVisibility => IsTickable ? Visibility.Visible : Visibility.Collapsed;

    public Visibility TimeVisibility => HasTime ? Visibility.Visible : Visibility.Collapsed;

    public Visibility LocationVisibility => string.IsNullOrWhiteSpace(Location) ? Visibility.Collapsed : Visibility.Visible;

    public Visibility CalendarVisibility => string.IsNullOrWhiteSpace(CalendarName) ? Visibility.Collapsed : Visibility.Visible;

    public double CompletedOpacity => IsCompleted ? 0.55 : 1;

    public string CompletionGlyph => IsCompleted ? "\uE73E" : string.Empty;

    public Visibility CompletionVisibility => IsCompleted ? Visibility.Visible : Visibility.Collapsed;

    partial void OnIsCompletedChanged(bool value)
    {
        OnPropertyChanged(nameof(CompletedOpacity));
        OnPropertyChanged(nameof(CompletionGlyph));
        OnPropertyChanged(nameof(CompletionVisibility));
    }
}

public sealed class AgendaDayViewModel : ObservableObject
{
    public required string Header { get; init; }

    public required string Weekday { get; init; }

    public required string DayText { get; init; }

    public required bool IsToday { get; init; }

    public required IReadOnlyList<AgendaItemViewModel> Items { get; init; }
}
