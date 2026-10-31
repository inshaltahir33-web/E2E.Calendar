using E2E.Calendar.Core.Models;

namespace E2E.Calendar.Core.Services;

/// <summary>
/// Lays out local occurrences for week and day views without any UI dependencies.
/// </summary>
public static class CalendarLayout
{
    /// <summary>Timed reminders and tasks occupy a fixed 30-minute visual block.</summary>
    public static readonly TimeSpan ReminderVisualDuration = TimeSpan.FromMinutes(30);

    private static readonly TimeSpan OneDay = TimeSpan.FromDays(1);

    /// <summary>
    /// Lays out every occurrence that touches <paramref name="rangeStart"/>..<paramref name="rangeEnd"/> (inclusive).
    /// All-day events and events longer than a day become week banners; everything else is split into timed
    /// blocks per calendar day it touches. A timed item that only crosses midnight (for example 22:00-02:00)
    /// is split across both days instead of becoming a banner.
    /// Reminders and tasks are always a single-day 30-minute block at their due time, whatever end
    /// date or duration their data carries.
    /// </summary>
    public static CalendarLayoutResult Build(
        IEnumerable<LayoutOccurrence> occurrences,
        DateOnly rangeStart,
        DateOnly rangeEnd,
        WeekStartDay weekStart = WeekStartDay.Monday,
        int maxBannerLanes = 2,
        int maxColumns = 3)
    {
        ArgumentNullException.ThrowIfNull(occurrences);

        if (rangeEnd < rangeStart)
        {
            throw new ArgumentException("The range end must not be before the range start.", nameof(rangeEnd));
        }

        if (maxBannerLanes < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maxBannerLanes), maxBannerLanes, "At least one banner lane is required.");
        }

        if (maxColumns < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maxColumns), maxColumns, "At least one column is required.");
        }

        var banners = new List<BannerSpan>();
        var timedByDay = new Dictionary<DateOnly, List<TimedSegment>>();

        foreach (var occurrence in occurrences)
        {
            if (occurrence is null)
            {
                continue;
            }

            var isChip = occurrence.Kind is CalendarItemKind.Reminder or CalendarItemKind.Task;

            if (occurrence.IsAllDay && !isChip)
            {
                var startDay = DateOnly.FromDateTime(occurrence.StartLocal);
                var endDay = occurrence.EndLocal > occurrence.StartLocal
                    ? InclusiveEndDay(occurrence.EndLocal)
                    : startDay;

                banners.Add(new BannerSpan(occurrence, startDay, MaxDate(startDay, endDay)));
                continue;
            }

            var end = isChip
                ? occurrence.StartLocal + ReminderVisualDuration
                : occurrence.EndLocal;

            if (end <= occurrence.StartLocal)
            {
                continue;
            }

            if (isChip)
            {
                // A chip belongs to the day it is due on, even when it is due near midnight.
                var dayEnd = DateOnly.FromDateTime(occurrence.StartLocal).ToDateTime(TimeOnly.MinValue).AddDays(1);

                if (end > dayEnd)
                {
                    end = dayEnd;
                }
            }
            else if (end - occurrence.StartLocal > OneDay)
            {
                var startDay = DateOnly.FromDateTime(occurrence.StartLocal);

                banners.Add(new BannerSpan(occurrence, startDay, MaxDate(startDay, InclusiveEndDay(end))));
                continue;
            }

            AddTimedSegments(occurrence, end, rangeStart, rangeEnd, timedByDay);
        }

        return new CalendarLayoutResult(
            BuildWeeks(banners, rangeStart, rangeEnd, weekStart, maxBannerLanes),
            BuildDays(timedByDay, rangeStart, rangeEnd, maxColumns));
    }

    /// <summary>
    /// Converts a stored item to the local occurrence shape used by this layout.
    /// Date-only tasks still report <see cref="LayoutOccurrence.IsAllDay"/>, but
    /// <see cref="Build"/> renders reminders and tasks as chips at their due time.
    /// </summary>
    public static LayoutOccurrence ToOccurrence(CalendarItem item, TimeZoneInfo timeZone)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(timeZone);

        return new LayoutOccurrence
        {
            Id = item.Id,
            StartLocal = timeZone.ToLocal(item.StartUtc),
            EndLocal = timeZone.ToLocal(item.EndUtc),
            IsAllDay = item.AllDay || (item.Kind == CalendarItemKind.Task && !item.HasTime),
            Kind = item.Kind,
        };
    }

    private static DateOnly InclusiveEndDay(DateTime end) =>
        end.TimeOfDay == TimeSpan.Zero
            ? DateOnly.FromDateTime(end).AddDays(-1)
            : DateOnly.FromDateTime(end);

    private static DateOnly MaxDate(DateOnly a, DateOnly b) => a > b ? a : b;

    private static void AddTimedSegments(
        LayoutOccurrence occurrence,
        DateTime end,
        DateOnly rangeStart,
        DateOnly rangeEnd,
        Dictionary<DateOnly, List<TimedSegment>> timedByDay)
    {
        var start = occurrence.StartLocal;
        var day = DateOnly.FromDateTime(start);

        if (day < rangeStart)
        {
            day = rangeStart;
        }

        while (day <= rangeEnd)
        {
            var dayStart = day.ToDateTime(TimeOnly.MinValue);
            var dayEnd = dayStart.AddDays(1);

            var clippedStart = start > dayStart ? start : dayStart;
            var clippedEnd = end < dayEnd ? end : dayEnd;

            if (clippedEnd > clippedStart)
            {
                if (!timedByDay.TryGetValue(day, out var segments))
                {
                    segments = [];
                    timedByDay[day] = segments;
                }

                segments.Add(new TimedSegment(occurrence, clippedStart, clippedEnd));
            }

            if (dayEnd >= end)
            {
                break;
            }

            day = day.AddDays(1);
        }
    }

    private static List<WeekRowLayout> BuildWeeks(
        List<BannerSpan> banners,
        DateOnly rangeStart,
        DateOnly rangeEnd,
        WeekStartDay weekStart,
        int maxBannerLanes)
    {
        var weeks = new List<WeekRowLayout>();

        for (var rowStart = CalendarPeriod.StartOfWeek(rangeStart, weekStart); rowStart <= rangeEnd; rowStart = rowStart.AddDays(7))
        {
            var rowEnd = rowStart.AddDays(6);

            var candidates = banners
                .Where(banner => banner.EndDay >= rowStart && banner.StartDay <= rowEnd)
                .Select(banner => ClipToVisible(banner, rowStart, rowEnd, rangeStart, rangeEnd))
                .Where(banner => banner.StartDay <= banner.EndDay)
                .OrderBy(banner => banner.StartDay)
                .ThenByDescending(banner => banner.EndDay)
                .ThenBy(banner => banner.Span.Source.Id, StringComparer.Ordinal)
                .ToList();

            var laneEnds = new List<DateOnly>();
            var assignments = new List<(ClippedBanner Banner, int Lane)>(candidates.Count);

            foreach (var candidate in candidates)
            {
                var lane = laneEnds.FindIndex(end => end < candidate.StartDay);

                if (lane < 0)
                {
                    lane = laneEnds.Count;
                    laneEnds.Add(candidate.EndDay);
                }
                else
                {
                    laneEnds[lane] = candidate.EndDay;
                }

                assignments.Add((candidate, lane));
            }

            var visible = new List<BannerPlacement>();
            var hiddenPlacements = new List<BannerPlacement>();
            var hiddenByDay = new Dictionary<DateOnly, int>();

            foreach (var (banner, lane) in assignments)
            {
                var placement = new BannerPlacement(
                    banner.Span.Source,
                    banner.StartDay,
                    banner.EndDay,
                    lane,
                    ContinuesFromPrev: banner.StartDay > banner.Span.StartDay,
                    ContinuesToNext: banner.EndDay < banner.Span.EndDay);

                if (lane < maxBannerLanes)
                {
                    visible.Add(placement);
                }
                else
                {
                    hiddenPlacements.Add(placement);

                    for (var day = banner.StartDay; day <= banner.EndDay; day = day.AddDays(1))
                    {
                        hiddenByDay[day] = hiddenByDay.GetValueOrDefault(day) + 1;
                    }
                }
            }

            var hidden = hiddenByDay
                .OrderBy(pair => pair.Key)
                .Select(pair => new BannerOverflow(pair.Key, pair.Value))
                .ToList();

            var orderedVisible = visible
                .OrderBy(banner => banner.Lane)
                .ThenBy(banner => banner.StartDay)
                .ThenBy(banner => banner.Occurrence.Id, StringComparer.Ordinal)
                .ToList();

            var orderedHidden = hiddenPlacements
                .OrderBy(banner => banner.Lane)
                .ThenBy(banner => banner.StartDay)
                .ThenBy(banner => banner.Occurrence.Id, StringComparer.Ordinal)
                .ToList();

            weeks.Add(new WeekRowLayout(rowStart, rowEnd, orderedVisible, hidden, orderedHidden));
        }

        return weeks;
    }

    /// <summary>
    /// Clips a banner to the week row and to the requested day range, so a day
    /// view only receives the part of the banner that is actually on screen.
    /// Continuation flags stay relative to the original occurrence.
    /// </summary>
    private static ClippedBanner ClipToVisible(
        BannerSpan span,
        DateOnly rowStart,
        DateOnly rowEnd,
        DateOnly rangeStart,
        DateOnly rangeEnd)
    {
        var start = span.StartDay > rowStart ? span.StartDay : rowStart;
        var end = span.EndDay < rowEnd ? span.EndDay : rowEnd;

        if (start < rangeStart)
        {
            start = rangeStart;
        }

        if (end > rangeEnd)
        {
            end = rangeEnd;
        }

        return new ClippedBanner(span, start, end);
    }

    private static List<DayColumnLayout> BuildDays(
        Dictionary<DateOnly, List<TimedSegment>> timedByDay,
        DateOnly rangeStart,
        DateOnly rangeEnd,
        int maxColumns)
    {
        var days = new List<DayColumnLayout>();

        for (var day = rangeStart; day <= rangeEnd; day = day.AddDays(1))
        {
            var placements = new List<TimedPlacement>();
            var hidden = new List<TimedPlacement>();
            var overflow = 0;

            if (timedByDay.TryGetValue(day, out var segments) && segments.Count > 0)
            {
                var ordered = segments
                    .OrderBy(segment => segment.Start)
                    .ThenByDescending(segment => segment.End)
                    .ThenBy(segment => segment.Source.Id, StringComparer.Ordinal)
                    .ToList();

                var cluster = new List<TimedSegment>();
                var clusterEnd = DateTime.MinValue;

                foreach (var segment in ordered)
                {
                    if (cluster.Count > 0 && segment.Start >= clusterEnd)
                    {
                        overflow = PackCluster(cluster, maxColumns, placements, hidden, overflow);
                        cluster.Clear();
                        clusterEnd = DateTime.MinValue;
                    }

                    if (segment.End > clusterEnd)
                    {
                        clusterEnd = segment.End;
                    }

                    cluster.Add(segment);
                }

                if (cluster.Count > 0)
                {
                    overflow = PackCluster(cluster, maxColumns, placements, hidden, overflow);
                }
            }

            days.Add(new DayColumnLayout(day, placements, overflow, hidden));
        }

        return days;
    }

    private static int PackCluster(
        List<TimedSegment> cluster,
        int maxColumns,
        List<TimedPlacement> placements,
        List<TimedPlacement> hidden,
        int overflow)
    {
        var columnEnds = new List<DateTime>(cluster.Count);
        var columns = new int[cluster.Count];

        for (var index = 0; index < cluster.Count; index++)
        {
            var segment = cluster[index];
            var column = columnEnds.FindIndex(end => end <= segment.Start);

            if (column < 0)
            {
                column = columnEnds.Count;
                columnEnds.Add(segment.End);
            }
            else
            {
                columnEnds[column] = segment.End;
            }

            columns[index] = column;
        }

        var columnCount = Math.Min(columnEnds.Count, maxColumns);

        for (var index = 0; index < cluster.Count; index++)
        {
            var segment = cluster[index];

            if (columns[index] < maxColumns)
            {
                placements.Add(new TimedPlacement(
                    segment.Source,
                    segment.Start,
                    segment.End,
                    columns[index],
                    columnCount));
            }
            else
            {
                hidden.Add(new TimedPlacement(
                    segment.Source,
                    segment.Start,
                    segment.End,
                    columns[index],
                    columnCount));
                overflow++;
            }
        }

        return overflow;
    }

    private sealed record BannerSpan(LayoutOccurrence Source, DateOnly StartDay, DateOnly EndDay);

    private sealed record TimedSegment(LayoutOccurrence Source, DateTime Start, DateTime End);

    private sealed record ClippedBanner(BannerSpan Span, DateOnly StartDay, DateOnly EndDay);
}

/// <summary>A local occurrence handed to <see cref="CalendarLayout"/>.</summary>
public sealed record LayoutOccurrence
{
    public required string Id { get; init; }

    /// <summary>Local wall-clock start.</summary>
    public required DateTime StartLocal { get; init; }

    /// <summary>Local wall-clock end. For all-day items an end at midnight is exclusive.</summary>
    public required DateTime EndLocal { get; init; }

    public bool IsAllDay { get; init; }

    public CalendarItemKind Kind { get; init; } = CalendarItemKind.Event;
}

public sealed record CalendarLayoutResult(
    IReadOnlyList<WeekRowLayout> Weeks,
    IReadOnlyList<DayColumnLayout> Days);

public sealed record WeekRowLayout(
    DateOnly Start,
    DateOnly End,
    IReadOnlyList<BannerPlacement> Banners,
    IReadOnlyList<BannerOverflow> HiddenBanners,
    IReadOnlyList<BannerPlacement> HiddenPlacements);

public sealed record BannerPlacement(
    LayoutOccurrence Occurrence,
    DateOnly StartDay,
    DateOnly EndDay,
    int Lane,
    bool ContinuesFromPrev,
    bool ContinuesToNext);

public sealed record BannerOverflow(DateOnly Date, int HiddenCount);

public sealed record DayColumnLayout(
    DateOnly Date,
    IReadOnlyList<TimedPlacement> Items,
    int OverflowCount,
    IReadOnlyList<TimedPlacement> Hidden);

public sealed record TimedPlacement(
    LayoutOccurrence Occurrence,
    DateTime VisualStart,
    DateTime VisualEnd,
    int Column,
    int ColumnCount);
