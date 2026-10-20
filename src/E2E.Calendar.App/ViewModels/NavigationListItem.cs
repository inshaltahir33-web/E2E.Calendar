using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;

namespace E2E.Calendar.App.ViewModels;

public sealed partial class NavigationListItem : ObservableObject
{
    [ObservableProperty]
    private int _count;

    [ObservableProperty]
    private bool _isSelected;

    public required string Label { get; init; }

    public required string Glyph { get; init; }

    public required string TargetKey { get; init; }

    public Visibility AccentVisibility => IsSelected ? Visibility.Visible : Visibility.Collapsed;

    partial void OnIsSelectedChanged(bool value)
    {
        OnPropertyChanged(nameof(AccentVisibility));
    }
}
