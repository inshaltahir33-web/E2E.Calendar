using E2E.Calendar.Core.Abstractions;
using E2E.Calendar.Core.Caching;
using E2E.Calendar.Core.Models;

namespace E2E.Calendar.Core.Services;

public sealed class EventQueryService : IEventQueryCache
{
    public const int DefaultCacheCapacity = 16;

    private readonly IEventRepository _events;
    private readonly IRecurrenceExpander _expander;
    private readonly LruCache<QueryKey, IReadOnlyList<Occurrence>> _cache;

    public EventQueryService(
        IEventRepository events,
        IRecurrenceExpander expander,
        int cacheCapacity = DefaultCacheCapacity)
    {
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(expander);

        _events = events;
        _expander = expander;
        _cache = new LruCache<QueryKey, IReadOnlyList<Occurrence>>(cacheCapacity);
    }

    public async Task<IReadOnlyList<Occurrence>> GetOccurrencesAsync(
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        string? calendarId = null,
        CancellationToken cancellationToken = default)
    {
        var key = new QueryKey(calendarId, fromUtc, toUtc);

        if (_cache.TryGet(key, out var cached))
        {
            return cached;
        }

        var events = await _events.GetInRangeAsync(calendarId, fromUtc, toUtc, cancellationToken);
        var occurrences = new List<Occurrence>();

        foreach (var calendarEvent in events)
        {
            if (string.IsNullOrWhiteSpace(calendarEvent.RRule))
            {
                if (calendarEvent.StartUtc < toUtc && calendarEvent.EndUtc > fromUtc)
                {
                    occurrences.Add(new Occurrence
                    {
                        EventId = calendarEvent.Id,
                        StartUtc = calendarEvent.StartUtc,
                        EndUtc = calendarEvent.EndUtc,
                        IsException = false,
                    });
                }

                continue;
            }

            var exceptions = await _events.GetExceptionsAsync(
                calendarEvent.Id,
                fromUtc,
                toUtc,
                cancellationToken);

            try
            {
                occurrences.AddRange(_expander.Expand(calendarEvent, exceptions, fromUtc, toUtc));
            }
            catch (FormatException)
            {
            }
            catch (ArgumentException)
            {
            }
        }

        var result = occurrences
            .OrderBy(occurrence => occurrence.StartUtc)
            .ThenBy(occurrence => occurrence.EventId, StringComparer.Ordinal)
            .ToList();

        _cache.Set(key, result);

        return result;
    }

    public void Invalidate() => _cache.Clear();

    private sealed record QueryKey(string? CalendarId, DateTimeOffset FromUtc, DateTimeOffset ToUtc);
}
