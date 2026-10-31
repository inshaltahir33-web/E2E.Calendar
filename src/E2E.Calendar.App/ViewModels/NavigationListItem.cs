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

    public string? TargetParameter { get; init; }

    /// <summary>Set for named task lists; null for Inbox and the smart views.</summary>
    public string? ListId { get; init; }

    /// <summary>Named lists can be renamed or deleted; Inbox cannot.</summary>
    public bool IsUserList { get; init; }

    public Visibility AccentVisibility => IsSelected ? Visibility.Visible : Visibility.Collapsed;

    partial void OnIsSelectedChanged(bool value)
    {
        OnPropertyChanged(nameof(AccentVisibility));
    }
}
