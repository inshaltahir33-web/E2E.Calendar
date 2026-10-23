namespace E2E.Calendar.Core.Models;

/// <summary>
/// The twelve calendar palette names defined by docs/11-THEME.md. Calendars store a
/// palette name, never a raw colour; the App resolves names to theme brushes.
/// </summary>
public static class CalendarPalette
{
    public const string Indigo = "Indigo";
    public const string Blue = "Blue";
    public const string Teal = "Teal";
    public const string Green = "Green";
    public const string Lime = "Lime";
    public const string Amber = "Amber";
    public const string Orange = "Orange";
    public const string Red = "Red";
    public const string Pink = "Pink";
    public const string Purple = "Purple";
    public const string Slate = "Slate";
    public const string Brown = "Brown";

    /// <summary>Fallback used whenever a stored colour cannot be resolved.</summary>
    public const string Fallback = Slate;

    public const string Personal = Blue;
    public const string Work = Indigo;
    public const string Family = Pink;
    public const string Health = Teal;
    public const string Travel = Orange;
    public const string Finance = Green;
    public const string Social = Pink;
    public const string Learning = Amber;
    public const string Focus = Blue;
    public const string Birthday = Red;
    public const string Holiday = Purple;
    public const string Other = Slate;

    public static IReadOnlyList<string> All { get; } =
    [
        Indigo,
        Blue,
        Teal,
        Green,
        Lime,
        Amber,
        Orange,
        Red,
        Pink,
        Purple,
        Slate,
        Brown,
    ];

    /// <summary>
    /// Pre-theme hex values written by earlier builds. Kept only to migrate stored
    /// calendars/events to the nearest palette name (docs/11 §4).
    /// </summary>
    private static readonly Dictionary<string, string> LegacyHex = new(StringComparer.OrdinalIgnoreCase)
    {
        ["#0078D4"] = Blue,
        ["#8764B8"] = Purple,
        ["#C239B3"] = Pink,
        ["#00B7C3"] = Teal,
        ["#FF8C00"] = Orange,
        ["#00B050"] = Green,
        ["#E3008C"] = Pink,
        ["#986F0B"] = Amber,
        ["#004E8C"] = Blue,
        ["#E81123"] = Red,
        ["#6A00C4"] = Purple,
        ["#767676"] = Slate,
    };

    public static bool IsKnown(string? value) =>
        !string.IsNullOrWhiteSpace(value) &&
        All.Any(name => string.Equals(name, value.Trim(), StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Maps a stored value to a palette name. Accepts palette names (any casing) and
    /// legacy hex values; anything unrecognised falls back to <see cref="Fallback"/>.
    /// </summary>
    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Fallback;
        }

        var trimmed = value.Trim();

        foreach (var name in All)
        {
            if (string.Equals(name, trimmed, StringComparison.OrdinalIgnoreCase))
            {
                return name;
            }
        }

        return LegacyHex.TryGetValue(trimmed, out var mapped) ? mapped : Fallback;
    }
}
