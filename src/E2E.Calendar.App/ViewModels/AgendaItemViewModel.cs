using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;

namespace E2E.Calendar.App.ViewModels;

public sealed class AgendaItemViewModel : ObservableObject
{
    public required string Title { get; init; }

    public string? Color { get; init; }

    public bool IsDone { get; init; }

    public bool IsTickable { get; init; }

    public string TimeLabel { get; init; } = string.Empty;

    public Visibility TickVisibility => IsTickable ? Visibility.Visible : Visibility.Collapsed;
}

public sealed class AgendaDayViewModel : ObservableObject
{
    public required string Header { get; init; }

    public required IReadOnlyList<AgendaItemViewModel> Items { get; init; }
}
