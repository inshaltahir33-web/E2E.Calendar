using E2E.Calendar.Core.Abstractions;
using E2E.Calendar.Core.Models;

namespace E2E.Calendar.Core.Services;

public sealed class SchedulingAssistantService
{
    private readonly IEventRepository _events;

    public SchedulingAssistantService(IEventRepository events)
    {
        _events = events;
    }

    public async Task<IReadOnlyList<MeetingSlot>> FindSlotsAsync(
        IReadOnlyList<string> calendarIds,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        TimeSpan duration,
        TimeSpan workingDayStart,
        TimeSpan workingDayEnd,
        TimeSpan slotIncrement,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(calendarIds);
        if (toUtc <= fromUtc || duration <= TimeSpan.Zero || slotIncrement <= TimeSpan.Zero ||
            workingDayEnd <= workingDayStart)
        {
            throw new ArgumentOutOfRangeException(nameof(duration), "Scheduling constraints are invalid.");
        }

        var busy = new List<(DateTimeOffset Start, DateTimeOffset End)>();
        foreach (var calendarId in calendarIds.Distinct(StringComparer.Ordinal))
        {
            var events = await _events.GetInRangeAsync(calendarId, fromUtc, toUtc, cancellationToken);
            busy.AddRange(events.Where(item => !item.Deleted).Select(item => (item.StartUtc, item.EndUtc)));
        }

        var merged = Merge(busy);
        var slots = new List<MeetingSlot>();
        for (var date = DateOnly.FromDateTime(fromUtc.UtcDateTime); date <= DateOnly.FromDateTime(toUtc.UtcDateTime); date = date.AddDays(1))
        {
            var cursor = new DateTimeOffset(date.ToDateTime(TimeOnly.FromTimeSpan(workingDayStart)), TimeSpan.Zero);
            var dayEnd = new DateTimeOffset(date.ToDateTime(TimeOnly.FromTimeSpan(workingDayEnd)), TimeSpan.Zero);
            while (cursor + duration <= dayEnd && cursor + duration <= toUtc)
            {
                if (cursor >= fromUtc && !merged.Any(item => item.Start < cursor + duration && item.End > cursor))
                {
                    slots.Add(new MeetingSlot { StartUtc = cursor, EndUtc = cursor + duration });
                }

                cursor += slotIncrement;
            }
        }

        return slots;
    }

    private static IReadOnlyList<(DateTimeOffset Start, DateTimeOffset End)> Merge(
        IEnumerable<(DateTimeOffset Start, DateTimeOffset End)> ranges)
    {
        var result = new List<(DateTimeOffset Start, DateTimeOffset End)>();
        foreach (var range in ranges.Where(item => item.End > item.Start).OrderBy(item => item.Start))
        {
            if (result.Count == 0 || range.Start > result[^1].End)
            {
                result.Add(range);
            }
            else
            {
                var current = result[^1];
                result[^1] = (current.Start, current.End > range.End ? current.End : range.End);
            }
        }

        return result;
    }
}
