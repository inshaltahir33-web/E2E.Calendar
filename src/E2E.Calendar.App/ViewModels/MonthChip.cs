using CommunityToolkit.Mvvm.ComponentModel;
namespace E2E.Calendar.App.ViewModels;

public sealed class MonthChip : ObservableObject
{
    public required string Title { get; init; }

    public string? Color { get; init; }
}
