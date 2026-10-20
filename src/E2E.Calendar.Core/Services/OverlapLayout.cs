using E2E.Calendar.Core.Models;

namespace E2E.Calendar.Core.Services;

public sealed record PackedItem(
    CalendarItem Item,
    int Column,
    int ColumnCount,
    double StartFraction,
    double EndFraction,
    int ClusterIndex = 0);

public static class OverlapLayout
{
    public static IReadOnlyList<PackedItem> Pack(IEnumerable<CalendarItem> items) =>
        Pack(items, null, null);

    public static IReadOnlyList<PackedItem> Pack(
        IEnumerable<CalendarItem> items,
        DateTimeOffset? rangeStartUtc,
        DateTimeOffset? rangeEndUtc)
    {
        ArgumentNullException.ThrowIfNull(items);

        var ordered = items
            .Where(item => item.EndUtc > item.StartUtc)
            .OrderBy(item => item.StartUtc)
            .ThenByDescending(item => item.EndUtc)
            .ThenBy(item => item.Id, StringComparer.Ordinal)
            .ToList();

        var packed = new List<PackedItem>(ordered.Count);

        var clusterIndex = 0;

        foreach (var cluster in BuildClusters(ordered))
        {
            var columnEnds = new List<DateTimeOffset>();
            var assignments = new List<int>();

            foreach (var item in cluster)
            {
                var column = columnEnds.FindIndex(end => end <= item.StartUtc);

                if (column < 0)
                {
                    column = columnEnds.Count;
                    columnEnds.Add(item.EndUtc);
                }
                else
                {
                    columnEnds[column] = item.EndUtc;
                }

                assignments.Add(column);
            }

            for (var index = 0; index < cluster.Count; index++)
            {
                var item = cluster[index];

                packed.Add(new PackedItem(
                    item,
                    assignments[index],
                    columnEnds.Count,
                    Fraction(item.StartUtc, rangeStartUtc, rangeEndUtc),
                    Fraction(item.EndUtc, rangeStartUtc, rangeEndUtc),
                    clusterIndex));
            }

            clusterIndex++;
        }

        return packed;
    }

    private static IReadOnlyList<List<CalendarItem>> BuildClusters(IReadOnlyList<CalendarItem> ordered)
    {
        var clusters = new List<List<CalendarItem>>();
        var current = new List<CalendarItem>();
        var clusterEnd = DateTimeOffset.MinValue;

        foreach (var item in ordered)
        {
            if (current.Count > 0 && item.StartUtc >= clusterEnd)
            {
                clusters.Add(current);
                current = new List<CalendarItem>();
            }

            if (item.EndUtc > clusterEnd)
            {
                clusterEnd = item.EndUtc;
            }

            current.Add(item);
        }

        if (current.Count > 0)
        {
            clusters.Add(current);
        }

        return clusters;
    }

    private static double Fraction(DateTimeOffset value, DateTimeOffset? rangeStartUtc, DateTimeOffset? rangeEndUtc)
    {
        if (rangeStartUtc is null || rangeEndUtc is null)
        {
            return 0;
        }

        var total = (rangeEndUtc.Value - rangeStartUtc.Value).TotalMilliseconds;

        if (total <= 0)
        {
            return 0;
        }

        var offset = (value - rangeStartUtc.Value).TotalMilliseconds / total;

        return Math.Clamp(offset, 0, 1);
    }
}
