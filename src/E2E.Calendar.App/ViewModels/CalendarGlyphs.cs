namespace E2E.Calendar.App.ViewModels;

public static class CalendarGlyphs
{
    public static string Today { get; } = Glyph(0xE787);

    public static string Scheduled { get; } = Glyph(0xE823);

    public static string Overdue { get; } = Glyph(0xE783);

    public static string Completed { get; } = Glyph(0xE73E);

    public static string Flagged { get; } = Glyph(0xE734);

    public static string Inbox { get; } = Glyph(0xE8A5);

    public static string Work { get; } = Glyph(0xE7B3);

    public static string New { get; } = Glyph(0xE710);

    public static string Settings { get; } = Glyph(0xE713);

    public static string Sync { get; } = Glyph(0xE895);

    public static string Premium { get; } = Glyph(0xE734);

    public static string Day { get; } = Glyph(0xE7B3);

    public static string Week { get; } = Glyph(0xE7B4);

    public static string Month { get; } = Glyph(0xE7B5);

    public static string Agenda { get; } = Glyph(0xE8A5);

    public static string Tasks { get; } = Glyph(0xE9D5);

    public static string Menu { get; } = Glyph(0xE700);

    public static string Previous { get; } = Glyph(0xE72B);

    public static string Next { get; } = Glyph(0xE72A);

    private static string Glyph(int codePoint) => ((char)codePoint).ToString();
}
