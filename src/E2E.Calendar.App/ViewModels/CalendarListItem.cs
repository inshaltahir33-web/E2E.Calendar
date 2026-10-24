using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml.Media;

namespace E2E.Calendar.App.ViewModels;

public sealed partial class CalendarListItem : ObservableObject
{
    [ObservableProperty]
    private bool _isVisible = true;

    public required string Id { get; init; }

    public required string Name { get; init; }

    public required string Color { get; init; }

    public Brush ColorBrush { get; init; } = new SolidColorBrush();
}
