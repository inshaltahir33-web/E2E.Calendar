using System.Globalization;
using E2E.Calendar.Core;

namespace E2E.Calendar.Core.Models;

public sealed record CalendarPeriod(CalendarViewMode Mode, DateTimeOffset FromUtc, DateTimeOffset ToUtc)
{
    public const int AgendaDays = 30;

    public static CalendarPeriod For(CalendarViewMode mode, DateOnly localDate, string? timeZoneId, WeekStartDay weekStart = WeekStartDay.Monday)
    {
        var timeZone = timeZoneId.TryToTimeZoneInfo(out var resolved) ? resolved : TimeZoneInfo.Utc;
        var (fromLocal, toLocal) = LocalRange(mode, localDate, weekStart);

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

    public static string FormatLabel(CalendarViewMode mode, DateOnly localDate, string? cultureName = null, WeekStartDay weekStart = WeekStartDay.Monday)
    {
        var culture = string.IsNullOrWhiteSpace(cultureName)
            ? CultureInfo.CurrentCulture
            : CultureInfo.GetCultureInfo(cultureName);

        var date = localDate.ToDateTime(TimeOnly.MinValue);

        return mode switch
        {
            CalendarViewMode.Day => date.ToString("d MMM yyyy", culture),
            CalendarViewMode.Week => FormatWeekLabel(culture, localDate, weekStart),
            CalendarViewMode.Month => date.ToString("Y", culture),
            CalendarViewMode.Agenda => FormatAgendaLabel(culture, localDate, weekStart),
            _ => date.ToString("ddd d MMM yyyy", culture),
        };
    }

    private static string FormatAgendaLabel(CultureInfo culture, DateOnly localDate, WeekStartDay weekStart = WeekStartDay.Monday)
    {
        var date = localDate.ToDateTime(TimeOnly.MinValue);
        var start = date;
        var end = date.AddDays(AgendaDays - 1);

        // Always show "d MMM – d MMM yyyy" format.
        // This naturally shows both months when crossing a month,
        // and shows the year at the end; both years when crossing a year.
        return string.Format(
            culture,
            "{0} – {1}",
            start.ToString("d MMM", culture),
            end.ToString("d MMM yyyy", culture));
    }

    private static (DateTime From, DateTime To) LocalRange(CalendarViewMode mode, DateOnly localDate, WeekStartDay weekStart = WeekStartDay.Monday)
    {
        var dayStart = localDate.ToDateTime(TimeOnly.MinValue);

        return mode switch
        {
            CalendarViewMode.Day => (dayStart, dayStart.AddDays(1)),
            CalendarViewMode.Week => (StartOfWeek(localDate, weekStart).ToDateTime(TimeOnly.MinValue), StartOfWeek(localDate, weekStart).AddDays(7).ToDateTime(TimeOnly.MinValue)),
            CalendarViewMode.Month => (StartOfMonth(localDate).ToDateTime(TimeOnly.MinValue), StartOfMonth(localDate).AddMonths(1).ToDateTime(TimeOnly.MinValue)),
            CalendarViewMode.Agenda => (dayStart, dayStart.AddDays(AgendaDays)),
            _ => (dayStart, dayStart.AddDays(1)),
        };
    }

    private static string FormatWeekLabel(CultureInfo culture, DateOnly localDate, WeekStartDay weekStart = WeekStartDay.Monday)
    {
        var start = StartOfWeek(localDate, weekStart);
        var end = EndOfWeek(localDate, weekStart);

        // The start year is only shown when the week straddles a year boundary, so a
        // normal week reads "26 Oct – 1 Nov 2026" and a year-crossing one shows both.
        var startFormat = start.Year == end.Year ? "d MMM" : "d MMM yyyy";

        return string.Format(
            culture,
            "{0} – {1}",
            start.ToString(startFormat, culture),
            end.ToString("d MMM yyyy", culture));
    }
}
