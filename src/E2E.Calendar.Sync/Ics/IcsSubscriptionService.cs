using System.Net;
using E2E.Calendar.Core.Abstractions;
using E2E.Calendar.Core.Models;
using Microsoft.Extensions.Logging;
using CalendarModel = E2E.Calendar.Core.Models.Calendar;

namespace E2E.Calendar.Sync.Ics;

/// <summary>
/// Keeps read-only, URL-subscribed calendars in sync by downloading their
/// iCalendar feed with conditional requests and replacing the stored events.
/// </summary>
public sealed class IcsSubscriptionService
{
    public const int MinRefreshHours = 1;
    public const int MaxRefreshHours = 24;

    private readonly HttpClient _http;
    private readonly IcsService _ics;
    private readonly ICalendarRepository _calendars;
    private readonly IEventRepository _events;
    private readonly ICalendarSubscriptionRepository _subscriptions;
    private readonly IClock _clock;
    private readonly ILogger<IcsSubscriptionService> _logger;

    public IcsSubscriptionService(
        HttpClient http,
        IcsService ics,
        ICalendarRepository calendars,
        IEventRepository events,
        ICalendarSubscriptionRepository subscriptions,
        IClock clock,
        ILogger<IcsSubscriptionService> logger)
    {
        ArgumentNullException.ThrowIfNull(http);
        ArgumentNullException.ThrowIfNull(ics);
        ArgumentNullException.ThrowIfNull(calendars);
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(subscriptions);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(logger);

        _http = http;
        _ics = ics;
        _calendars = calendars;
        _events = events;
        _subscriptions = subscriptions;
        _clock = clock;
        _logger = logger;
    }

    public Task<IReadOnlyList<CalendarSubscription>> GetAllAsync(CancellationToken cancellationToken = default) =>
        _subscriptions.GetAllAsync(cancellationToken);

    public Task<CalendarSubscription?> GetAsync(string calendarId, CancellationToken cancellationToken = default) =>
        _subscriptions.GetAsync(calendarId, cancellationToken);

    /// <summary>
    /// Creates a read-only calendar for <paramref name="url"/>, stores the refresh
    /// interval, and imports the feed once. A failed first download removes the
    /// half-created subscription so the user can correct the URL and retry.
    /// </summary>
    public async Task<CalendarSubscription> AddAsync(
        string url,
        int refreshHours,
        CancellationToken cancellationToken = default)
    {
        if (!Uri.TryCreate(url?.Trim(), UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException("Enter a calendar URL that starts with http:// or https://.", nameof(url));
        }

        if (refreshHours is < MinRefreshHours or > MaxRefreshHours)
        {
            throw new ArgumentOutOfRangeException(
                nameof(refreshHours),
                $"Refresh interval must be between {MinRefreshHours} and {MaxRefreshHours} hours.");
        }

        var calendar = new CalendarModel
        {
            Id = $"ics-{Guid.NewGuid():N}",
            Name = NameFrom(uri),
            Color = CalendarPalette.Other,
            Visible = true,
            ReadOnly = true,
            IsLocal = true,
        };

        var subscription = new CalendarSubscription
        {
            CalendarId = calendar.Id,
            Url = uri.ToString(),
            RefreshHours = refreshHours,
        };

        await _calendars.UpsertAsync(calendar, cancellationToken);
        await _subscriptions.UpsertAsync(subscription, cancellationToken);

        try
        {
            var result = await RefreshAsync(subscription, cancellationToken);
            return result.Subscription;
        }
        catch
        {
            await RemoveAsync(calendar.Id, CancellationToken.None);
            throw;
        }
    }

    /// <summary>
    /// Downloads one subscription with <c>If-None-Match</c>/<c>If-Modified-Since</c>.
    /// A 304 only advances the last-refresh time; a 200 replaces every event in
    /// the calendar with the feed contents.
    /// </summary>
    public async Task<SubscriptionRefreshResult> RefreshAsync(
        CalendarSubscription subscription,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(subscription);

        var calendar = await _calendars.GetByIdAsync(subscription.CalendarId, cancellationToken)
            ?? throw new InvalidOperationException("The subscribed calendar no longer exists.");

        using var request = new HttpRequestMessage(HttpMethod.Get, subscription.Url);

        if (!string.IsNullOrWhiteSpace(subscription.ETag))
        {
            request.Headers.TryAddWithoutValidation("If-None-Match", subscription.ETag);
        }

        if (subscription.LastModifiedUtc is { } lastModified)
        {
            request.Headers.IfModifiedSince = lastModified;
        }

        using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        var now = _clock.UtcNow;
        var etag = response.Headers.ETag?.ToString();

        if (response.StatusCode == HttpStatusCode.NotModified)
        {
            var unchanged = subscription with
            {
                ETag = etag ?? subscription.ETag,
                LastModifiedUtc = response.Content.Headers.LastModified ?? subscription.LastModifiedUtc,
                LastRefreshUtc = now,
            };
            await _subscriptions.UpsertAsync(unchanged, cancellationToken);

            return new SubscriptionRefreshResult(unchanged, false, 0, now);
        }

        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        var events = _ics.Import(content, calendar.Id, now)
            .Select(item => item with
            {
                // UIDs are only unique within a feed, so scope them to the calendar.
                Id = $"{calendar.Id}:{item.Id}",
                CalendarId = calendar.Id,
                Dirty = false,
            })
            .ToList();

        await _events.DeleteByCalendarIdsAsync([calendar.Id], cancellationToken);

        foreach (var calendarEvent in events)
        {
            await _events.UpsertAsync(calendarEvent, cancellationToken);
        }

        var refreshed = subscription with
        {
            ETag = etag,
            LastModifiedUtc = response.Content.Headers.LastModified,
            LastRefreshUtc = now,
        };
        await _subscriptions.UpsertAsync(refreshed, cancellationToken);

        _logger.LogInformation(
            "Refreshed ICS subscription {CalendarId} with {Count} event(s).",
            calendar.Id,
            events.Count);

        return new SubscriptionRefreshResult(refreshed, true, events.Count, now);
    }

    /// <summary>Refreshes every subscription whose interval has elapsed.</summary>
    public async Task<int> RefreshDueAsync(CancellationToken cancellationToken = default)
    {
        var now = _clock.UtcNow;
        var refreshed = 0;

        foreach (var subscription in await _subscriptions.GetAllAsync(cancellationToken))
        {
            if (subscription.LastRefreshUtc is { } last &&
                now - last < TimeSpan.FromHours(subscription.RefreshHours))
            {
                continue;
            }

            try
            {
                await RefreshAsync(subscription, cancellationToken);
                refreshed++;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "ICS subscription refresh failed for {CalendarId}.",
                    subscription.CalendarId);
            }
        }

        return refreshed;
    }

    public async Task RemoveAsync(string calendarId, CancellationToken cancellationToken = default)
    {
        await _events.DeleteByCalendarIdsAsync([calendarId], cancellationToken);
        await _subscriptions.DeleteAsync(calendarId, cancellationToken);
        await _calendars.DeleteAsync(calendarId, cancellationToken);
    }

    private static string NameFrom(Uri uri)
    {
        var segment = uri.Segments.LastOrDefault()?.Trim('/');

        if (!string.IsNullOrWhiteSpace(segment))
        {
            var name = Uri.UnescapeDataString(segment);

            if (name.EndsWith(".ics", StringComparison.OrdinalIgnoreCase))
            {
                name = name[..^4];
            }

            if (!string.IsNullOrWhiteSpace(name))
            {
                return name;
            }
        }

        return uri.Host;
    }
}

public sealed record SubscriptionRefreshResult(
    CalendarSubscription Subscription,
    bool Changed,
    int EventCount,
    DateTimeOffset RefreshedUtc);
