using E2E.Calendar.Core.Models;

namespace E2E.Calendar.Core.Services;

public static class MiniCalendarGridBuilder
{
    public const int DaysPerWeek = 7;

    public static MiniCalendarGrid Build(
        int year,
        int month,
        WeekStartDay weekStart,
        DateOnly today,
        DateOnly? selectedDate,
        IReadOnlyDictionary<DateOnly, int>? eventCounts = null)
    {
        if (month < 1 || month > 12)
        {
            throw new ArgumentOutOfRangeException(nameof(month), month, "The month must be between 1 and 12.");
        }

        var firstOfMonth = new DateOnly(year, month, 1);
        var daysInMonth = DateTime.DaysInMonth(year, month);
        var leadingDays = DaysBetween(firstOfMonth.DayOfWeek, weekStart);
        var weekCount = (leadingDays + daysInMonth + DaysPerWeek - 1) / DaysPerWeek;
        var gridStart = firstOfMonth.AddDays(-leadingDays);
        var gridEnd = gridStart.AddDays((weekCount * DaysPerWeek) - 1);

        var selectedWeekStart = selectedDate is { } selected
            ? StartOfWeek(selected, weekStart)
            : (DateOnly?)null;

        var weeks = new List<MiniCalendarWeek>(weekCount);

        for (var week = 0; week < weekCount; week++)
        {
            var days = new List<MiniCalendarDay>(DaysPerWeek);

            for (var day = 0; day < DaysPerWeek; day++)
            {
                var date = gridStart.AddDays((week * DaysPerWeek) + day);
                var count = eventCounts is not null && eventCounts.TryGetValue(date, out var value) ? value : 0;

                days.Add(new MiniCalendarDay
                {
                    Date = date,
                    IsCurrentMonth = date.Month == month && date.Year == year,
                    IsToday = date == today,
                    IsSelected = selectedDate == date,
                    IsInSelectedWeek = selectedWeekStart is { } weekStartDate && date >= weekStartDate && date < weekStartDate.AddDays(DaysPerWeek),
                    EventCount = count,
                });
            }

            weeks.Add(new MiniCalendarWeek(days));
        }

        return new MiniCalendarGrid(weeks, gridStart, gridEnd);
    }

    public static DateOnly StartOfWeek(DateOnly date, WeekStartDay weekStart)
    {
        var offset = DaysBetween(date.DayOfWeek, weekStart);

        return date.AddDays(-offset);
    }

    private static int DaysBetween(DayOfWeek day, WeekStartDay weekStart) =>
        ((int)day - (int)weekStart + DaysPerWeek) % DaysPerWeek;
}
