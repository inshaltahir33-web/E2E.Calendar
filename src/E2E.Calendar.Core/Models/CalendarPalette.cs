namespace E2E.Calendar.Core.Models;

public static class CalendarPalette
{
    public const string Personal = "#0078D4";
    public const string Work = "#8764B8";
    public const string Family = "#C239B3";
    public const string Health = "#00B7C3";
    public const string Travel = "#FF8C00";
    public const string Finance = "#00B050";
    public const string Social = "#E3008C";
    public const string Learning = "#986F0B";
    public const string Focus = "#004E8C";
    public const string Birthday = "#E81123";
    public const string Holiday = "#6A00C4";
    public const string Other = "#767676";

    public static IReadOnlyList<string> All { get; } =
    [
        Personal,
        Work,
        Family,
        Health,
        Travel,
        Finance,
        Social,
        Learning,
        Focus,
        Birthday,
        Holiday,
        Other,
    ];
}
