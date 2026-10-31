using System.Net;
using System.Net.Http.Headers;
using E2E.Calendar.Core.Abstractions;
using E2E.Calendar.Core.Models;
using E2E.Calendar.Sync.Ics;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using CalendarModel = E2E.Calendar.Core.Models.Calendar;

namespace E2E.Calendar.Sync.Tests;

public sealed class IcsSubscriptionServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 31, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset LastModified =
        new(2026, 10, 20, 8, 0, 0, TimeSpan.Zero);

    private const string Feed = """
        BEGIN:VCALENDAR
        VERSION:2.0
        PRODID:-//E2E//Test//EN
        BEGIN:VEVENT
        UID:event-1
        SUMMARY:Planning
        DTSTART:20261025T090000Z
        DTEND:20261025T100000Z
        END:VEVENT
        END:VCALENDAR
        """;

    [Fact]
    public async Task Add_imports_feed_and_marks_calendar_read_only()
    {
        var fixture = new Fixture(Ok(Feed));

        var subscription = await fixture.Service.AddAsync("https://example.com/team.ics", 6);

        subscription.RefreshHours.Should().Be(6);
        subscription.ETag.Should().Be("\"v1\"");
        subscription.LastModifiedUtc.Should().Be(LastModified);
        subscription.LastRefreshUtc.Should().Be(Now);

        var calendar = await fixture.Calendars.GetByIdAsync(subscription.CalendarId);
        calendar.Should().NotBeNull();
        calendar!.Name.Should().Be("team");
        calendar.ReadOnly.Should().BeTrue();
        calendar.IsLocal.Should().BeTrue();

        var stored = await fixture.Subscriptions.GetAsync(subscription.CalendarId);
        stored.Should().Be(subscription);

        var events = await fixture.Events.GetInRangeAsync(
            subscription.CalendarId,
            Now.AddYears(-1),
            Now.AddYears(1));
        events.Should().ContainSingle();
        events[0].Id.Should().Be($"{subscription.CalendarId}:event-1");
        events[0].Dirty.Should().BeFalse();

        fixture.Handler.Requests.Should().ContainSingle();
        fixture.Handler.Requests[0].Headers.IfNoneMatch.Should().BeEmpty();
        fixture.Handler.Requests[0].Headers.IfModifiedSince.Should().BeNull();
    }

    [Fact]
    public async Task Add_uses_the_supplied_calendar_name()
    {
        var fixture = new Fixture(Ok(Feed));

        var subscription = await fixture.Service.AddAsync("https://example.com/team.ics", 6, "  Team holidays  ");

        var calendar = await fixture.Calendars.GetByIdAsync(subscription.CalendarId);
        calendar!.Name.Should().Be("Team holidays");
    }

    [Fact]
    public async Task Refresh_sends_conditional_headers_and_keeps_events_on_304()
    {
        var fixture = new Fixture(new HttpResponseMessage(HttpStatusCode.NotModified));
        var subscription = await fixture.SeedAsync(events: [ExistingEvent("cal-ics", "old")]);

        var result = await fixture.Service.RefreshAsync(subscription);

        result.Changed.Should().BeFalse();
        result.EventCount.Should().Be(0);
        result.Subscription.LastRefreshUtc.Should().Be(Now);

        var request = fixture.Handler.Requests.Should().ContainSingle().Subject;
        request.Headers.IfNoneMatch.Should().Contain(new EntityTagHeaderValue("\"v1\""));
        request.Headers.IfModifiedSince.Should().Be(LastModified);

        var events = await fixture.Events.GetInRangeAsync("cal-ics", Now.AddYears(-1), Now.AddYears(1));
        events.Should().ContainSingle();
        events[0].Title.Should().Be("old");
    }

    [Fact]
    public async Task Refresh_replaces_all_events_when_feed_changes()
    {
        var fixture = new Fixture(Ok(Feed));
        var subscription = await fixture.SeedAsync(events:
        [
            ExistingEvent("cal-ics", "old-one"),
            ExistingEvent("cal-ics", "old-two"),
        ]);

        var result = await fixture.Service.RefreshAsync(subscription);

        result.Changed.Should().BeTrue();
        result.EventCount.Should().Be(1);

        var events = await fixture.Events.GetInRangeAsync("cal-ics", Now.AddYears(-1), Now.AddYears(1));
        events.Should().ContainSingle();
        events[0].Title.Should().Be("Planning");
        events[0].Id.Should().Be("cal-ics:event-1");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(25)]
    public async Task Add_rejects_refresh_interval_outside_one_to_24_hours(int hours)
    {
        var fixture = new Fixture(Ok(Feed));

        var act = () => fixture.Service.AddAsync("https://example.com/team.ics", hours);

        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
        fixture.Handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task Add_rolls_back_when_the_initial_download_fails()
    {
        var fixture = new Fixture(new HttpResponseMessage(HttpStatusCode.InternalServerError));

        var act = () => fixture.Service.AddAsync("https://example.com/broken.ics", 6);

        await act.Should().ThrowAsync<HttpRequestException>();
        (await fixture.Calendars.GetAllAsync()).Should().BeEmpty();
        (await fixture.Subscriptions.GetAllAsync()).Should().BeEmpty();
    }

    private static HttpResponseMessage Ok(string body)
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(body),
        };
        response.Headers.ETag = new EntityTagHeaderValue("\"v1\"");
        response.Content.Headers.LastModified = LastModified;

        return response;
    }

    private static CalendarEvent ExistingEvent(string calendarId, string title) => new()
    {
        Id = $"{calendarId}:{title}",
        CalendarId = calendarId,
        Title = title,
        StartUtc = new DateTimeOffset(2026, 10, 25, 9, 0, 0, TimeSpan.Zero),
        EndUtc = new DateTimeOffset(2026, 10, 25, 10, 0, 0, TimeSpan.Zero),
        TimeZoneId = "UTC",
        CreatedUtc = Now,
        UpdatedUtc = Now,
    };

    private sealed class Fixture
    {
        public Fixture(HttpResponseMessage response)
        {
            Handler = new StubHandler(response);
            Calendars = new FakeCalendarRepository();
            Events = new FakeEventRepository();
            Subscriptions = new FakeSubscriptionRepository();
            Service = new IcsSubscriptionService(
                new HttpClient(Handler),
                new IcsService(),
                Calendars,
                Events,
                Subscriptions,
                new FakeClock(Now),
                NullLogger<IcsSubscriptionService>.Instance);
        }

        public StubHandler Handler { get; }

        public FakeCalendarRepository Calendars { get; }

        public FakeEventRepository Events { get; }

        public FakeSubscriptionRepository Subscriptions { get; }

        public IcsSubscriptionService Service { get; }

        public async Task<CalendarSubscription> SeedAsync(IReadOnlyList<CalendarEvent> events)
        {
            await Calendars.UpsertAsync(new CalendarModel
            {
                Id = "cal-ics",
                Name = "Team",
                ReadOnly = true,
                IsLocal = true,
            });

            foreach (var calendarEvent in events)
            {
                await Events.UpsertAsync(calendarEvent);
            }

            var subscription = new CalendarSubscription
            {
                CalendarId = "cal-ics",
                Url = "https://example.com/team.ics",
                RefreshHours = 6,
                ETag = "\"v1\"",
                LastModifiedUtc = LastModified,
                LastRefreshUtc = Now.AddHours(-7),
            };
            await Subscriptions.UpsertAsync(subscription);

            return subscription;
        }
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses = new();

        public StubHandler(params HttpResponseMessage[] responses)
        {
            foreach (var response in responses)
            {
                _responses.Enqueue(response);
            }
        }

        public List<HttpRequestMessage> Requests { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);

            return _responses.Count > 0
                ? Task.FromResult(_responses.Dequeue())
                : throw new InvalidOperationException("No HTTP response was queued.");
        }
    }

    private sealed class FakeClock : IClock
    {
        public FakeClock(DateTimeOffset utcNow) => UtcNow = utcNow;

        public DateTimeOffset UtcNow { get; }

        public TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }

    private sealed class FakeCalendarRepository : ICalendarRepository
    {
        private readonly Dictionary<string, CalendarModel> _calendars = new();

        public Task<IReadOnlyList<CalendarModel>> GetAllAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<CalendarModel>>(_calendars.Values.ToArray());

        public Task<CalendarModel?> GetByIdAsync(string id, CancellationToken cancellationToken = default) =>
            Task.FromResult(_calendars.GetValueOrDefault(id));

        public Task UpsertAsync(CalendarModel calendar, CancellationToken cancellationToken = default)
        {
            _calendars[calendar.Id] = calendar;
            return Task.CompletedTask;
        }

        public Task DeleteAsync(string id, CancellationToken cancellationToken = default)
        {
            if (_calendars.TryGetValue(id, out var calendar) && calendar.IsLocal && calendar.AccountId is null)
            {
                _calendars.Remove(id);
            }

            return Task.CompletedTask;
        }

        public Task DeleteByAccountAsync(string accountId, CancellationToken cancellationToken = default)
        {
            foreach (var id in _calendars.Values.Where(item => item.AccountId == accountId).Select(item => item.Id))
            {
                _calendars.Remove(id);
            }

            return Task.CompletedTask;
        }
    }

    private sealed class FakeSubscriptionRepository : ICalendarSubscriptionRepository
    {
        private readonly Dictionary<string, CalendarSubscription> _subscriptions = new();

        public Task<IReadOnlyList<CalendarSubscription>> GetAllAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<CalendarSubscription>>(_subscriptions.Values.ToArray());

        public Task<CalendarSubscription?> GetAsync(string calendarId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_subscriptions.GetValueOrDefault(calendarId));

        public Task UpsertAsync(CalendarSubscription subscription, CancellationToken cancellationToken = default)
        {
            _subscriptions[subscription.CalendarId] = subscription;
            return Task.CompletedTask;
        }

        public Task DeleteAsync(string calendarId, CancellationToken cancellationToken = default)
        {
            _subscriptions.Remove(calendarId);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeEventRepository : IEventRepository
    {
        private readonly Dictionary<string, CalendarEvent> _events = new();

        public Task<CalendarEvent?> GetByIdAsync(string id, CancellationToken cancellationToken = default) =>
            Task.FromResult(_events.GetValueOrDefault(id));

        public Task<IReadOnlyList<CalendarEvent>> GetInRangeAsync(
            string? calendarId,
            DateTimeOffset fromUtc,
            DateTimeOffset toUtc,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<CalendarEvent>>(_events.Values
                .Where(item =>
                    (calendarId is null || item.CalendarId == calendarId) &&
                    item.StartUtc < toUtc &&
                    item.EndUtc > fromUtc)
                .OrderBy(item => item.StartUtc)
                .ToArray());

        public Task<IReadOnlyList<CalendarEvent>> SearchAsync(
            EventSearchCriteria criteria,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<CalendarEvent>>([]);

        public Task<IReadOnlyList<CalendarEvent>> GetSoftDeletedAsync(
            DateTimeOffset deletedBeforeUtc,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<CalendarEvent>>([]);

        public Task<IReadOnlyList<CalendarEvent>> GetExceptionsAsync(
            string masterId,
            DateTimeOffset fromUtc,
            DateTimeOffset toUtc,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<CalendarEvent>>([]);

        public Task UpsertAsync(CalendarEvent calendarEvent, CancellationToken cancellationToken = default)
        {
            _events[calendarEvent.Id] = calendarEvent;
            return Task.CompletedTask;
        }

        public Task DeleteByCalendarIdsAsync(
            IReadOnlyList<string> calendarIds,
            CancellationToken cancellationToken = default)
        {
            foreach (var id in _events.Values
                         .Where(item => calendarIds.Contains(item.CalendarId))
                         .Select(item => item.Id)
                         .ToArray())
            {
                _events.Remove(id);
            }

            return Task.CompletedTask;
        }
    }
}
