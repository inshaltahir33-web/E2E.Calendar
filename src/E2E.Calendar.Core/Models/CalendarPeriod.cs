using System.Globalization;
using E2E.Calendar.Core;

namespace E2E.Calendar.Core.Models;

public sealed record CalendarPeriod(CalendarViewMode Mode, DateTimeOffset FromUtc, DateTimeOffset ToUtc)
{
    public const int AgendaDays = 30;

    public static CalendarPeriod For(CalendarViewMode mode, DateOnly localDate, string? timeZoneId)
    {
        var timeZone = timeZoneId.TryToTimeZoneInfo(out var resolved) ? resolved : TimeZoneInfo.Utc;
        var (fromLocal, toLocal) = LocalRange(mode, localDate);

        return new CalendarPeriod(mode, timeZone.ToUtc(fromLocal), timeZone.ToUtc(toLocal));
    }

    public static DateOnly StartOfWeek(DateOnly date) => StartOfWeek(date, WeekStartDay.Monday);

    public static DateOnly StartOfWeek(DateOnly date, WeekStartDay weekStart)
    {
        var offset = ((int)date.DayOfWeek - (int)weekStart + 7) % 7;

        return date.AddDays(-offset);
    }

    public static DateOnly EndOfWeek(DateOnly date) => EndOfWeek(date, WeekStartDay.Monday);

    public static DateOnly EndOfWeek(DateOnly date, WeekStartDay weekStart) =>
        StartOfWeek(date, weekStart).AddDays(6);

    public static DateOnly StartOfMonth(DateOnly date) => new(date.Year, date.Month, 1);

    public static DateOnly EndOfMonth(DateOnly date) => StartOfMonth(date).AddMonths(1).AddDays(-1);

    public static string FormatLabel(CalendarViewMode mode, DateOnly localDate, string? cultureName = null)
    {
        var culture = string.IsNullOrWhiteSpace(cultureName)
            ? CultureInfo.CurrentCulture
            : CultureInfo.GetCultureInfo(cultureName);

        var date = localDate.ToDateTime(TimeOnly.MinValue);

        return mode switch
        {
            CalendarViewMode.Day => date.ToString("D", culture),
            CalendarViewMode.Week => FormatWeekLabel(culture, localDate),
            CalendarViewMode.Month => date.ToString("Y", culture),
            CalendarViewMode.Agenda => string.Format(culture, "Next {0} days", AgendaDays),
            _ => date.ToString("D", culture),
        };
    }

    private static (DateTime From, DateTime To) LocalRange(CalendarViewMode mode, DateOnly localDate)
    {
        var dayStart = localDate.ToDateTime(TimeOnly.MinValue);

        return mode switch
        {
            CalendarViewMode.Day => (dayStart, dayStart.AddDays(1)),
            CalendarViewMode.Week => (StartOfWeek(localDate).ToDateTime(TimeOnly.MinValue), StartOfWeek(localDate).AddDays(7).ToDateTime(TimeOnly.MinValue)),
            CalendarViewMode.Month => (StartOfMonth(localDate).ToDateTime(TimeOnly.MinValue), StartOfMonth(localDate).AddMonths(1).ToDateTime(TimeOnly.MinValue)),
            CalendarViewMode.Agenda => (dayStart, dayStart.AddDays(AgendaDays)),
            _ => (dayStart, dayStart.AddDays(1)),
        };
    }

    private static string FormatWeekLabel(CultureInfo culture, DateOnly localDate)
    {
        var start = StartOfWeek(localDate);
        var end = EndOfWeek(localDate);

        if (start.Month == end.Month && start.Year == end.Year)
        {
            return string.Format(culture, "{0} – {1}", start.Day, end.ToString("d MMM yyyy", culture));
        }

        return string.Format(
            culture,
            "{0} – {1}",
            start.ToString("d MMM", culture),
            end.ToString("d MMM yyyy", culture));
    }
}
