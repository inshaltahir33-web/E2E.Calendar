using E2E.Calendar.Core.Models;

namespace E2E.Calendar.Core.Services;

public sealed record DemoScheduledEvent(
    string Calendar,
    string Title,
    DateOnly Date,
    int Hour,
    int Minute,
    int DurationMinutes,
    string Color,
    string? RRule = null);

/// <summary>
/// Demo content that repeats. It is kept in Core so a test can prove the
/// recurring series are defined exactly once instead of once per displayed
/// week (which duplicated every occurrence in Day, Week and Agenda).
/// </summary>
public static class DemoSchedule
{
    public static IReadOnlyList<DemoScheduledEvent> Recurring(DateOnly firstMonday) =>
        new[]
        {
            new DemoScheduledEvent(
                "Work",
                "Daily standup",
                firstMonday,
                9,
                30,
                15,
                CalendarPalette.Work,
                "FREQ=WEEKLY;BYDAY=MO,TU,WE,TH,FR"),
            new DemoScheduledEvent(
                "Personal",
                "Gym",
                firstMonday.AddDays(1),
                18,
                0,
                60,
                CalendarPalette.Health,
                "FREQ=WEEKLY;BYDAY=TU,TH"),
        };
}
