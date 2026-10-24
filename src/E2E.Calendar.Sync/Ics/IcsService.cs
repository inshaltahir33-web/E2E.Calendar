using System.Globalization;
using E2E.Calendar.Core.Models;
using Ical.Net;
using Ical.Net.CalendarComponents;
using Ical.Net.DataTypes;
using Ical.Net.Serialization;
using CoreCalendarEvent = E2E.Calendar.Core.Models.CalendarEvent;
using IcalCalendarEvent = Ical.Net.CalendarComponents.CalendarEvent;
using IcalCalendar = Ical.Net.Calendar;

namespace E2E.Calendar.Sync.Ics;

public sealed class IcsService
{
    public string Export(IEnumerable<CoreCalendarEvent> events)
    {
        var calendar = new IcalCalendar();

        foreach (var source in events.Where(item => !item.Deleted))
        {
            var item = new IcalCalendarEvent
            {
                Uid = source.Id,
                Summary = source.Title,
                Description = source.Notes,
                Location = source.Location,
                DtStart = new CalDateTime(source.StartUtc.UtcDateTime, "UTC"),
                DtEnd = new CalDateTime(source.EndUtc.UtcDateTime, "UTC"),
            };

            if (!string.IsNullOrWhiteSpace(source.RRule))
            {
                item.RecurrenceRule = new RecurrencePattern(
                    source.RRule.StartsWith("RRULE:", StringComparison.OrdinalIgnoreCase)
                        ? source.RRule["RRULE:".Length..]
                        : source.RRule);
            }

            calendar.Events.Add(item);
        }

        return new CalendarSerializer(calendar).SerializeToString() ?? string.Empty;
    }

    public IReadOnlyList<CoreCalendarEvent> Import(string ics, string calendarId, DateTimeOffset nowUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ics);
        ArgumentException.ThrowIfNullOrWhiteSpace(calendarId);

        var calendar = IcalCalendar.Load(ics)
            ?? throw new FormatException("The iCalendar file could not be parsed.");
        return calendar.Events.Select(item => ToModel(item, calendarId, nowUtc)).ToList();
    }

    private static CoreCalendarEvent ToModel(IcalCalendarEvent source, string calendarId, DateTimeOffset nowUtc)
    {
        var start = source.DtStart?.Value is { } startValue
            ? new DateTimeOffset(startValue, TimeSpan.Zero)
            : throw new FormatException("ICS event has no DTSTART.");
        var end = source.DtEnd?.Value is { } endValue
            ? new DateTimeOffset(endValue, TimeSpan.Zero)
            : start.AddHours(1);
        var rule = source.RecurrenceRule?.ToString();

        return new CoreCalendarEvent
        {
            Id = string.IsNullOrWhiteSpace(source.Uid) ? Guid.NewGuid().ToString("N") : source.Uid,
            CalendarId = calendarId,
            RemoteId = null,
            ETag = null,
            Title = source.Summary ?? "(untitled)",
            Notes = source.Description,
            Location = source.Location,
            StartUtc = start.ToUniversalTime(),
            EndUtc = end.ToUniversalTime(),
            AllDay = source.DtStart?.HasTime == false,
            TimeZoneId = "UTC",
            RRule = rule,
            CreatedUtc = nowUtc,
            UpdatedUtc = nowUtc,
            Dirty = true,
        };
    }
}
