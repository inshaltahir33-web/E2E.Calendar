using E2E.Calendar.Core.Models;

namespace E2E.Calendar.Core.Services;

public static class HolidayOverrides
{
    public static IReadOnlyList<CalendarEvent> Apply(IEnumerable<CalendarEvent> holidays, IEnumerable<HolidayOverride> overrides) =>
        HolidayOverrideApplier.Apply(holidays, overrides);
    public static IReadOnlyList<CalendarEvent> Apply(IEnumerable<CalendarEvent> holidays, IEnumerable<HolidayOverride> overrides, string? countryCode) =>
        HolidayOverrideApplier.Apply(holidays, overrides, countryCode);
    public static IReadOnlyList<CalendarEvent> Apply(IEnumerable<HolidayOverride> overrides, IEnumerable<CalendarEvent> holidays) =>
        HolidayOverrideApplier.Apply(overrides, holidays);
    public static IReadOnlyList<CalendarEvent> Apply(IEnumerable<HolidayOverride> overrides, IEnumerable<CalendarEvent> holidays, string? countryCode) =>
        HolidayOverrideApplier.Apply(overrides, holidays, countryCode);

    public static IReadOnlyList<CalendarEvent> ApplyOverrides(IEnumerable<CalendarEvent> holidays, IEnumerable<HolidayOverride> overrides) =>
        HolidayOverrideApplier.ApplyOverrides(holidays, overrides);
    public static IReadOnlyList<CalendarEvent> ApplyOverrides(IEnumerable<CalendarEvent> holidays, IEnumerable<HolidayOverride> overrides, string? countryCode) =>
        HolidayOverrideApplier.ApplyOverrides(holidays, overrides, countryCode);
    public static IReadOnlyList<CalendarEvent> ApplyOverrides(IEnumerable<HolidayOverride> overrides, IEnumerable<CalendarEvent> holidays) =>
        HolidayOverrideApplier.ApplyOverrides(overrides, holidays);
    public static IReadOnlyList<CalendarEvent> ApplyOverrides(IEnumerable<HolidayOverride> overrides, IEnumerable<CalendarEvent> holidays, string? countryCode) =>
        HolidayOverrideApplier.ApplyOverrides(overrides, holidays, countryCode);
}

public sealed class HolidayOverrideService
{
    public HolidayOverrideService() { }

    public static IReadOnlyList<CalendarEvent> Apply(IEnumerable<CalendarEvent> holidays, IEnumerable<HolidayOverride> overrides) =>
        HolidayOverrideApplier.Apply(holidays, overrides);
    public static IReadOnlyList<CalendarEvent> Apply(IEnumerable<CalendarEvent> holidays, IEnumerable<HolidayOverride> overrides, string? countryCode) =>
        HolidayOverrideApplier.Apply(holidays, overrides, countryCode);
    public static IReadOnlyList<CalendarEvent> Apply(IEnumerable<HolidayOverride> overrides, IEnumerable<CalendarEvent> holidays) =>
        HolidayOverrideApplier.Apply(overrides, holidays);
    public static IReadOnlyList<CalendarEvent> Apply(IEnumerable<HolidayOverride> overrides, IEnumerable<CalendarEvent> holidays, string? countryCode) =>
        HolidayOverrideApplier.Apply(overrides, holidays, countryCode);

    public static IReadOnlyList<CalendarEvent> ApplyOverrides(IEnumerable<CalendarEvent> holidays, IEnumerable<HolidayOverride> overrides) =>
        HolidayOverrideApplier.ApplyOverrides(holidays, overrides);
    public static IReadOnlyList<CalendarEvent> ApplyOverrides(IEnumerable<CalendarEvent> holidays, IEnumerable<HolidayOverride> overrides, string? countryCode) =>
        HolidayOverrideApplier.ApplyOverrides(holidays, overrides, countryCode);
    public static IReadOnlyList<CalendarEvent> ApplyOverrides(IEnumerable<HolidayOverride> overrides, IEnumerable<CalendarEvent> holidays) =>
        HolidayOverrideApplier.ApplyOverrides(overrides, holidays);
    public static IReadOnlyList<CalendarEvent> ApplyOverrides(IEnumerable<HolidayOverride> overrides, IEnumerable<CalendarEvent> holidays, string? countryCode) =>
        HolidayOverrideApplier.ApplyOverrides(overrides, holidays, countryCode);
}

public static class HolidayCalendarOverrides
{
    public static IReadOnlyList<CalendarEvent> Apply(IEnumerable<CalendarEvent> holidays, IEnumerable<HolidayOverride> overrides) =>
        HolidayOverrideApplier.Apply(holidays, overrides);
    public static IReadOnlyList<CalendarEvent> Apply(IEnumerable<CalendarEvent> holidays, IEnumerable<HolidayOverride> overrides, string? countryCode) =>
        HolidayOverrideApplier.Apply(holidays, overrides, countryCode);
    public static IReadOnlyList<CalendarEvent> ApplyOverrides(IEnumerable<CalendarEvent> holidays, IEnumerable<HolidayOverride> overrides) =>
        HolidayOverrideApplier.ApplyOverrides(holidays, overrides);
    public static IReadOnlyList<CalendarEvent> ApplyOverrides(IEnumerable<CalendarEvent> holidays, IEnumerable<HolidayOverride> overrides, string? countryCode) =>
        HolidayOverrideApplier.ApplyOverrides(holidays, overrides, countryCode);
}