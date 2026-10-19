using E2E.Calendar.Core.Models;

namespace E2E.Calendar.Core.Recurrence;

public sealed record SeriesSplit(CalendarEvent Previous, CalendarEvent Following);

public static class SeriesSplitter
{
    public static SeriesSplit SplitSeries(
        CalendarEvent master,
        DateTimeOffset occurrenceStartUtc,
        string newRRule,
        DateTimeOffset? newStartUtc = null,
        string? newTitle = null,
        DateTimeOffset? nowUtc = null)
    {
        ArgumentNullException.ThrowIfNull(master);
        ArgumentException.ThrowIfNullOrWhiteSpace(newRRule);

        var timeZone = master.TimeZoneId.TryToTimeZoneInfo(out var resolved) ? resolved : TimeZoneInfo.Utc;
        var localOccurrence = TimeZoneInfo.ConvertTimeFromUtc(occurrenceStartUtc.UtcDateTime, timeZone);
        var localUntil = localOccurrence.AddSeconds(-1);

        var previous = master with { RRule = RRuleHelper.SetUntil(master.RRule, localUntil) };

        var start = newStartUtc ?? occurrenceStartUtc;
        var duration = master.EndUtc - master.StartUtc;
        var timestamp = nowUtc ?? master.UpdatedUtc;

        var following = master with
        {
            Id = Guid.NewGuid().ToString("N"),
            Title = newTitle ?? master.Title,
            RRule = newRRule,
            StartUtc = start,
            EndUtc = start + duration,
            RemoteId = null,
            ETag = null,
            RecurrenceMasterId = null,
            RecurrenceIdUtc = null,
            CreatedUtc = timestamp,
            UpdatedUtc = timestamp,
            Deleted = false,
            Dirty = true,
        };

        return new SeriesSplit(previous, following);
    }
}
