using E2E.Calendar.Core.Abstractions;
using E2E.Calendar.Core.Caching;
using E2E.Calendar.Core.Models;

namespace E2E.Calendar.Core.Services;

public sealed class EventQueryService : IEventQueryCache
{
    public const int DefaultCacheCapacity = 16;

    private readonly IEventRepository _events;
    private readonly IRecurrenceExpander _expander;
    private readonly LruCache<QueryKey, QueryResult> _cache;

    public EventQueryService(
        IEventRepository events,
        IRecurrenceExpander expander,
        int cacheCapacity = DefaultCacheCapacity)
    {
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(expander);

        _events = events;
        _expander = expander;
        _cache = new LruCache<QueryKey, QueryResult>(cacheCapacity);
    }

    public async Task<IReadOnlyList<Occurrence>> GetOccurrencesAsync(
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        string? calendarId = null,
        CancellationToken cancellationToken = default)
    {
        var result = await QueryAsync(fromUtc, toUtc, calendarId, cancellationToken);

        return result.Occurrences;
    }

    public async Task<IReadOnlyList<CalendarItem>> GetItemsAsync(
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        string? calendarId = null,
        CancellationToken cancellationToken = default)
    {
        var result = await QueryAsync(fromUtc, toUtc, calendarId, cancellationToken);

        return result.Occurrences
            .Select(occurrence => ToItem(occurrence, result.Sources))
            .ToList();
    }

    public void Invalidate() => _cache.Clear();

    private async Task<QueryResult> QueryAsync(
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        string? calendarId,
        CancellationToken cancellationToken)
    {
        var key = new QueryKey(calendarId, fromUtc, toUtc);

        if (_cache.TryGet(key, out var cached))
        {
            return cached;
        }

        var events = await _events.GetInRangeAsync(calendarId, fromUtc, toUtc, cancellationToken);
        var occurrences = new List<Occurrence>();
        var sources = new Dictionary<string, CalendarEvent>(StringComparer.Ordinal);

        foreach (var calendarEvent in events)
        {
            sources[calendarEvent.Id] = calendarEvent;

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

            foreach (var exception in exceptions)
            {
                sources[exception.Id] = exception;
            }

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

        var result = new QueryResult(
            occurrences
                .OrderBy(occurrence => occurrence.StartUtc)
                .ThenBy(occurrence => occurrence.EventId, StringComparer.Ordinal)
                .ToList(),
            sources);

        _cache.Set(key, result);

        return result;
    }

    private static CalendarItem ToItem(Occurrence occurrence, IReadOnlyDictionary<string, CalendarEvent> sources)
    {
        sources.TryGetValue(occurrence.EventId, out var source);

        return new CalendarItem
        {
            Id = occurrence.EventId,
            Title = source?.Title ?? string.Empty,
            StartUtc = occurrence.StartUtc,
            EndUtc = occurrence.EndUtc,
            Kind = CalendarItemKind.Event,
            CalendarId = source?.CalendarId,
            Color = source?.Color,
            AllDay = source?.AllDay ?? false,
            HasTime = source?.AllDay != true,
            IsException = occurrence.IsException,
            IsRecurring = !string.IsNullOrWhiteSpace(source?.RRule),
        };
    }

    private sealed record QueryKey(string? CalendarId, DateTimeOffset FromUtc, DateTimeOffset ToUtc);

    private sealed record QueryResult(
        IReadOnlyList<Occurrence> Occurrences,
        IReadOnlyDictionary<string, CalendarEvent> Sources);
}
