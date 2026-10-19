using CommunityToolkit.Mvvm.ComponentModel;
using E2E.Calendar.Core.Models;

namespace E2E.Calendar.App.ViewModels;

public sealed record AlertOption(string Label, int Minutes)
{
    public static IReadOnlyList<AlertOption> All { get; } =
    [
        new("At the start", 0),
        new("1 minute before", 1),
        new("5 minutes before", 5),
        new("10 minutes before", 10),
        new("15 minutes before", 15),
        new("30 minutes before", 30),
        new("1 hour before", 60),
        new("2 hours before", 120),
        new("1 day before", 1440),
    ];
}

public sealed partial class AlertRow : ObservableObject
{
    [ObservableProperty]
    private AlertOption _option = AlertOption.All[3];

    public required string Id { get; init; }

    public required OwnerType OwnerType { get; init; }

    public required string OwnerId { get; init; }

    public IReadOnlyList<AlertOption> Options => AlertOption.All;

    public int OffsetMinutes => Option.Minutes;
}
