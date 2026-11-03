using System.Globalization;
using System.Text;
using E2E.Calendar.Core;
using E2E.Calendar.Core.Models;
using E2E.Calendar.Core.Services;
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
    public string Export(IEnumerable<CoreCalendarEvent> events) =>
        Export(events, null);

    public string Export(IEnumerable<CoreCalendarEvent> events, IEnumerable<Alert>? alerts)
    {
        ArgumentNullException.ThrowIfNull(events);

        var alertLookup = (alerts ?? Array.Empty<Alert>())
            .Where(alert => alert.OwnerType == OwnerType.Event)
            .GroupBy(alert => alert.OwnerId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.Ordinal);

        var builder = new StringBuilder();
        builder.Append("BEGIN:VCALENDAR\r\n");
        builder.Append("VERSION:2.0\r\n");
        builder.Append("PRODID:-//BindLabs//Calendar Pro//EN\r\n");
        builder.Append("CALSCALE:GREGORIAN\r\n");
        builder.Append("METHOD:PUBLISH\r\n");

        foreach (var source in events.Where(item => !item.Deleted))
        {
            alertLookup.TryGetValue(source.Id, out var eventAlerts);
            AppendEvent(builder, source, eventAlerts ?? []);
        }

        builder.Append("END:VCALENDAR\r\n");
        return builder.ToString();
    }

    private static void AppendEvent(StringBuilder builder, CoreCalendarEvent source, IReadOnlyList<Alert> alerts)
    {
        builder.Append("BEGIN:VEVENT\r\n");
        AppendLine(builder, $"UID:{Escape(source.Id)}");
        AppendLine(builder, $"DTSTAMP:{FormatUtc(DateTimeOffset.UtcNow)}");
        AppendLine(builder, FormatStart(source));
        AppendLine(builder, FormatEnd(source));
        AppendLine(builder, $"SUMMARY:{Escape(source.Title)}");

        if (!string.IsNullOrWhiteSpace(source.Location))
        {
            AppendLine(builder, $"LOCATION:{Escape(source.Location)}");
        }

        var description = NotesText.ToPlainText(source.Notes);

        if (!string.IsNullOrWhiteSpace(description))
        {
            AppendLine(builder, $"DESCRIPTION:{Escape(description)}");
        }

        if (!string.IsNullOrWhiteSpace(source.RRule))
        {
            var rule = source.RRule.StartsWith("RRULE:", StringComparison.OrdinalIgnoreCase)
                ? source.RRule["RRULE:".Length..]
                : source.RRule;
            AppendLine(builder, $"RRULE:{rule}");
        }

        if (!string.IsNullOrWhiteSpace(source.ExDates))
        {
            var exDates = ExDateListToString(source.ExDates);
            if (!string.IsNullOrWhiteSpace(exDates))
            {
                AppendLine(builder, $"EXDATE:{exDates}");
            }
        }

        foreach (var alert in alerts)
        {
            AppendAlarm(builder, source, alert);
        }

        builder.Append("END:VEVENT\r\n");
    }

    private static void AppendAlarm(StringBuilder builder, CoreCalendarEvent source, Alert alert)
    {
        builder.Append("BEGIN:VALARM\r\n");
        builder.Append("ACTION:DISPLAY\r\n");
        builder.Append($"DESCRIPTION:{Escape(source.Title)}\r\n");

        if (alert.TriggerOffsetMinutes is { } offset)
        {
            AppendLine(builder, $"TRIGGER:-PT{Math.Abs(offset)}M");
        }
        else if (alert.TriggerAtUtc is { } at)
        {
            AppendLine(builder, $"TRIGGER;VALUE=DATE-TIME:{FormatUtc(at)}");
        }
        else
        {
            AppendLine(builder, "TRIGGER:-PT10M");
        }

        builder.Append("END:VALARM\r\n");
    }

    private static string FormatStart(CoreCalendarEvent source)
    {
        if (source.AllDay)
        {
            return $"DTSTART;VALUE=DATE:{source.StartUtc:yyyyMMdd}";
        }

        return $"DTSTART:{FormatUtc(source.StartUtc)}";
    }

    private static string FormatEnd(CoreCalendarEvent source)
    {
        if (source.AllDay)
        {
            var end = source.EndUtc.Date <= source.StartUtc.Date
                ? source.StartUtc.Date.AddDays(1)
                : source.EndUtc.Date;
            return $"DTEND;VALUE=DATE:{end:yyyyMMdd}";
        }

        return $"DTEND:{FormatUtc(source.EndUtc)}";
    }

    private static string FormatUtc(DateTimeOffset value) =>
        value.ToUniversalTime().ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);

    private static string Escape(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace(";", "\\;", StringComparison.Ordinal)
            .Replace(",", "\\,", StringComparison.Ordinal)
            .Replace("\r\n", "\\n", StringComparison.Ordinal)
            .Replace("\r", "\\n", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal);
    }

    private static void AppendLine(StringBuilder builder, string line)
    {
        builder.Append(Fold(line));
        builder.Append("\r\n");
    }

    private static string Fold(string line)
    {
        if (line.Length <= 75)
        {
            return line;
        }

        var result = new StringBuilder();
        var index = 0;
        var first = true;

        while (index < line.Length)
        {
            var chunkSize = first ? 75 : 74;
            var take = Math.Min(chunkSize, line.Length - index);
            var chunk = line.Substring(index, take);
            if (!first)
            {
                result.Append("\r\n ");
            }
            result.Append(chunk);
            index += take;
            first = false;
        }

        return result.ToString();
    }

    private static string ExDateListToString(string exDates)
    {
        var parts = exDates.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var formatted = new List<string>();

        foreach (var part in parts)
        {
            if (DateTimeOffset.TryParse(part, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var value))
            {
                formatted.Add(FormatUtc(value));
            }
            else
            {
                formatted.Add(part);
            }
        }

        return string.Join(",", formatted);
    }

    public IReadOnlyList<CoreCalendarEvent> ApplyHolidayOverrides(
        IEnumerable<CoreCalendarEvent> holidays,
        IEnumerable<HolidayOverride> overrides,
        string? countryCode = null) =>
        HolidayOverrideApplier.Apply(holidays, overrides, countryCode).ToList();

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