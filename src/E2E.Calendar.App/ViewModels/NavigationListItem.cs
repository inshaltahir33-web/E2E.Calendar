using CommunityToolkit.Mvvm.ComponentModel;

namespace E2E.Calendar.App.ViewModels;

public sealed partial class NavigationListItem : ObservableObject
{
    [ObservableProperty]
    private int _count;

    public required string Label { get; init; }

    public required string Glyph { get; init; }

    public required string TargetKey { get; init; }
}
