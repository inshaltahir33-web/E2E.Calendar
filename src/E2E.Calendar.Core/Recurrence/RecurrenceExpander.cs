using E2E.Calendar.Core.Abstractions;
using E2E.Calendar.Core.Models;
using IcalCalDateTime = Ical.Net.DataTypes.CalDateTime;
using IcalDuration = Ical.Net.DataTypes.Duration;
using IcalEvaluationOptions = Ical.Net.Evaluation.EvaluationOptions;
using IcalRecurrencePattern = Ical.Net.DataTypes.RecurrencePattern;
using IcalCalendarEvent = Ical.Net.CalendarComponents.CalendarEvent;

namespace E2E.Calendar.Core.Recurrence;

public sealed class RecurrenceExpander : IRecurrenceExpander
{
    public IEnumerable<Occurrence> Expand(
        CalendarEvent master,
        IEnumerable<CalendarEvent> exceptions,
        DateTimeOffset from,
        DateTimeOffset to)
    {
        ArgumentNullException.ThrowIfNull(master);
        ArgumentNullException.ThrowIfNull(exceptions);

        if (string.IsNullOrWhiteSpace(master.RRule) || to <= from)
        {
            return Array.Empty<Occurrence>();
        }

        var timeZone = ResolveTimeZone(master.TimeZoneId);
        var localStart = TimeZoneInfo.ConvertTimeFromUtc(master.StartUtc.UtcDateTime, timeZone);
        var localEnd = TimeZoneInfo.ConvertTimeFromUtc(master.EndUtc.UtcDateTime, timeZone);
        var wallDuration = localEnd - localStart;

        var exDates = ExDateList.Parse(master.ExDates);
        var exceptionByOriginalStart = new Dictionary<DateTimeOffset, CalendarEvent>();

        foreach (var exception in exceptions)
        {
            if (exception.RecurrenceIdUtc is { } originalStart)
            {
                exceptionByOriginalStart[originalStart] = exception;
            }
        }

        var toLocal = TimeZoneInfo.ConvertTimeFromUtc(to.UtcDateTime, timeZone);
        var searchFromLocal = TimeZoneInfo.ConvertTimeFromUtc((from - wallDuration).UtcDateTime, timeZone);

        var calendarEvent = new IcalCalendarEvent
        {
            DtStart = ToCalDateTime(localStart, timeZone),
            Duration = IcalDuration.FromTimeSpanExact(wallDuration),
        };

        calendarEvent.RecurrenceRule = new IcalRecurrencePattern(master.RRule);

        var occurrences = new List<Occurrence>();

        foreach (var occurrence in calendarEvent
                     .GetOccurrences(ToCalDateTime(searchFromLocal, timeZone), new IcalEvaluationOptions())
                     .TakeWhile(occurrence => occurrence.Period.StartTime.Value <= toLocal))
        {
            var occurrenceLocal = occurrence.Period.StartTime.Value;
            var occurrenceStartUtc = timeZone.ToUtc(occurrenceLocal);
            var occurrenceEndUtc = timeZone.ToUtc(occurrenceLocal + wallDuration);

            if (exceptionByOriginalStart.TryGetValue(occurrenceStartUtc, out var exception))
            {
                occurrences.Add(new Occurrence
                {
                    EventId = exception.Id,
                    StartUtc = exception.StartUtc,
                    EndUtc = exception.EndUtc,
                    IsException = true,
                });

                continue;
            }

            if (exDates.Contains(occurrenceStartUtc))
            {
                continue;
            }

            if (occurrenceStartUtc >= to || occurrenceEndUtc <= from)
            {
                continue;
            }

            occurrences.Add(new Occurrence
            {
                EventId = master.Id,
                StartUtc = occurrenceStartUtc,
                EndUtc = occurrenceEndUtc,
                IsException = false,
            });
        }

        return occurrences;
    }

    private static TimeZoneInfo ResolveTimeZone(string? timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId))
        {
            return TimeZoneInfo.Utc;
        }

        return timeZoneId.TryToTimeZoneInfo(out var timeZone) ? timeZone : TimeZoneInfo.Utc;
    }

    private static IcalCalDateTime ToCalDateTime(DateTime local, TimeZoneInfo timeZone) =>
        new(local.Year, local.Month, local.Day, local.Hour, local.Minute, local.Second, timeZone.Id);
}
