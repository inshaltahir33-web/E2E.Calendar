using E2E.Calendar.Core.Models;

namespace E2E.Calendar.Core.Services;

/// <summary>
/// Applies remote <see cref="HolidayOverride"/> entries on top of bundled holiday
/// events. Pure, deterministic and free of IO so it can be unit tested.
/// </summary>
public static class HolidayOverrideApplier
{
    public static IReadOnlyList<CalendarEvent> Apply(
        IEnumerable<CalendarEvent> holidays,
        IEnumerable<HolidayOverride> overrides) =>
        Apply(holidays, overrides, countryCode: null);

    public static IReadOnlyList<CalendarEvent> Apply(
        IEnumerable<CalendarEvent> holidays,
        IEnumerable<HolidayOverride> overrides,
        string? countryCode) =>
        ApplyCore(holidays, overrides, countryCode);

    public static IReadOnlyList<CalendarEvent> Apply(
        IEnumerable<HolidayOverride> overrides,
        IEnumerable<CalendarEvent> holidays) =>
        Apply(holidays, overrides, countryCode: null);

    public static IReadOnlyList<CalendarEvent> Apply(
        IEnumerable<HolidayOverride> overrides,
        IEnumerable<CalendarEvent> holidays,
        string? countryCode) =>
        Apply(holidays, overrides, countryCode);

    public static IReadOnlyList<CalendarEvent> ApplyOverrides(
        IEnumerable<CalendarEvent> holidays,
        IEnumerable<HolidayOverride> overrides) =>
        Apply(holidays, overrides, countryCode: null);

    public static IReadOnlyList<CalendarEvent> ApplyOverrides(
        IEnumerable<CalendarEvent> holidays,
        IEnumerable<HolidayOverride> overrides,
        string? countryCode) =>
        ApplyCore(holidays, overrides, countryCode);

    public static IReadOnlyList<CalendarEvent> ApplyOverrides(
        IEnumerable<HolidayOverride> overrides,
        IEnumerable<CalendarEvent> holidays) =>
        Apply(holidays, overrides, countryCode: null);

    public static IReadOnlyList<CalendarEvent> ApplyOverrides(
        IEnumerable<HolidayOverride> overrides,
        IEnumerable<CalendarEvent> holidays,
        string? countryCode) =>
        Apply(holidays, overrides, countryCode);

    public static IReadOnlyList<CalendarEvent> ApplyHolidayOverrides(
        IEnumerable<CalendarEvent> holidays,
        IEnumerable<HolidayOverride> overrides) =>
        Apply(holidays, overrides, countryCode: null);

    public static IReadOnlyList<CalendarEvent> ApplyHolidayOverrides(
        IEnumerable<CalendarEvent> holidays,
        IEnumerable<HolidayOverride> overrides,
        string? countryCode) =>
        ApplyCore(holidays, overrides, countryCode);

    private static IReadOnlyList<CalendarEvent> ApplyCore(
        IEnumerable<CalendarEvent> holidays,
        IEnumerable<HolidayOverride> overrides,
        string? countryCode)
    {
        ArgumentNullException.ThrowIfNull(holidays);
        ArgumentNullException.ThrowIfNull(overrides);

        var overrideList = overrides
            .Where(item => item is not null)
            .ToList();

        if (countryCode is not null)
        {
            overrideList = overrideList
                .Where(item => string.IsNullOrWhiteSpace(item.Country)
                    || string.Equals(item.Country, countryCode, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        var result = new List<CalendarEvent>();

        foreach (var holiday in holidays)
        {
            var country = countryCode ?? CountryFromCalendarId(holiday.CalendarId);
            var matching = overrideList.FirstOrDefault(item =>
                (string.IsNullOrWhiteSpace(item.Country) || string.IsNullOrWhiteSpace(country)
                    || string.Equals(item.Country, country, StringComparison.OrdinalIgnoreCase))
                && NamesMatch(item.Name, holiday.Title));

            if (matching is null)
            {
                result.Add(holiday);
                continue;
            }

            var newStart = new DateTimeOffset(
                matching.Date.ToDateTime(TimeOnly.MinValue),
                TimeSpan.Zero);
            var duration = holiday.EndUtc - holiday.StartUtc;
            if (duration <= TimeSpan.Zero)
            {
                duration = holiday.AllDay ? TimeSpan.FromDays(1) : TimeSpan.FromHours(1);
            }

            var newTitle = holiday.Title.Replace(" (approx.)", string.Empty, StringComparison.OrdinalIgnoreCase).Trim();

            result.Add(holiday with
            {
                Title = newTitle,
                StartUtc = newStart,
                EndUtc = newStart + duration,
            });
        }

        return result;
    }

    private static string? CountryFromCalendarId(string calendarId)
    {
        const string prefix = "holiday-";
        if (calendarId.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return calendarId[prefix.Length..];
        }

        return null;
    }

    private static bool NamesMatch(string overrideName, string holidayTitle)
    {
        if (string.IsNullOrWhiteSpace(overrideName))
        {
            return false;
        }

        var normalizedHoliday = holidayTitle
            .Replace(" (approx.)", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace(" (estimated)", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Trim();

        return string.Equals(normalizedHoliday, overrideName.Trim(), StringComparison.OrdinalIgnoreCase)
            || normalizedHoliday.StartsWith(overrideName.Trim() + " ", StringComparison.OrdinalIgnoreCase);
    }
}