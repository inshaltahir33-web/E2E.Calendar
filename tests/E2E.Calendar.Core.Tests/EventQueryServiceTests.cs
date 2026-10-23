using E2E.Calendar.Core.Abstractions;
using E2E.Calendar.Core.Models;
using E2E.Calendar.Core.Recurrence;
using E2E.Calendar.Core.Services;
using E2E.Calendar.Core.Sync;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace E2E.Calendar.Core.Tests;

public sealed class EventQueryServiceTests
{
    private static readonly DateTimeOffset From = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset To = new(2026, 2, 1, 0, 0, 0, TimeSpan.Zero);

    private readonly IEventRepository _events = Substitute.For<IEventRepository>();
    private readonly IOutbox _outbox = Substitute.For<IOutbox>();
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly RecurrenceExpander _expander = new();

    public EventQueryServiceTests()
    {
        _clock.UtcNow.Returns(new DateTimeOffset(2026, 1, 15, 9, 0, 0, TimeSpan.Zero));
    }

    private static CalendarEvent PlainEvent(string id, int day = 10) =>
        new()
        {
            Id = id,
            CalendarId = "cal-local",
            Title = id,
            StartUtc = new DateTimeOffset(2026, 1, day, 9, 0, 0, TimeSpan.Zero),
            EndUtc = new DateTimeOffset(2026, 1, day, 10, 0, 0, TimeSpan.Zero),
            TimeZoneId = "UTC",
            CreatedUtc = new DateTimeOffset(2025, 12, 1, 0, 0, 0, TimeSpan.Zero),
            UpdatedUtc = new DateTimeOffset(2025, 12, 1, 0, 0, 0, TimeSpan.Zero),
        };

    private static CalendarEvent RecurringEvent(string id = "master-1", string rRule = "FREQ=DAILY;COUNT=5") =>
        PlainEvent(id, 5) with { RRule = rRule };

    [Fact]
    public async Task Non_recurring_events_are_returned_as_occurrences()
    {
        _events.GetInRangeAsync("cal-local", From, To, Arg.Any<CancellationToken>())
            .Returns(new[] { PlainEvent("one"), PlainEvent("two", 12) });

        var service = new EventQueryService(_events, _expander);
        var occurrences = await service.GetOccurrencesAsync(From, To, "cal-local");

        occurrences.Select(o => o.EventId).Should().Equal("one", "two");
        occurrences.Should().OnlyContain(o => !o.IsException);
    }

    [Fact]
    public async Task Recurring_events_are_expanded()
    {
        _events.GetInRangeAsync("cal-local", From, To, Arg.Any<CancellationToken>())
            .Returns(new[] { RecurringEvent() });

        var service = new EventQueryService(_events, _expander);
        var occurrences = await service.GetOccurrencesAsync(From, To, "cal-local");

        occurrences.Should().HaveCount(5);
        occurrences.Select(o => o.StartUtc.Day).Should().Equal(5, 6, 7, 8, 9);
    }

    [Fact]
    public async Task Plain_and_recurring_events_are_merged_into_one_sorted_list()
    {
        _events.GetInRangeAsync("cal-local", From, To, Arg.Any<CancellationToken>())
            .Returns(new[] { RecurringEvent(), PlainEvent("plain", 7) });

        var service = new EventQueryService(_events, _expander);
        var occurrences = await service.GetOccurrencesAsync(From, To, "cal-local");

        occurrences.Should().HaveCount(6);
        occurrences.Select(o => o.StartUtc).Should().BeInAscendingOrder();
    }

    [Fact]
    public async Task The_exceptions_of_a_master_are_fetched_and_applied()
    {
        _events.GetInRangeAsync("cal-local", From, To, Arg.Any<CancellationToken>())
            .Returns(new[] { RecurringEvent() });

        var exception = PlainEvent("child-1", 7) with
        {
            RecurrenceMasterId = "master-1",
            RecurrenceIdUtc = new DateTimeOffset(2026, 1, 7, 9, 0, 0, TimeSpan.Zero),
        };

        _events.GetExceptionsAsync("master-1", From, To, Arg.Any<CancellationToken>())
            .Returns(new[] { exception });

        var service = new EventQueryService(_events, _expander);
        var occurrences = await service.GetOccurrencesAsync(From, To, "cal-local");

        occurrences.Should().HaveCount(5);
        occurrences.Single(o => o.IsException).EventId.Should().Be("child-1");
    }

    [Fact]
    public async Task An_exception_row_in_range_is_not_duplicated()
    {
        var master = RecurringEvent();
        var exception = PlainEvent("child-1", 7) with
        {
            RecurrenceMasterId = "master-1",
            RecurrenceIdUtc = new DateTimeOffset(2026, 1, 7, 9, 0, 0, TimeSpan.Zero),
        };

        _events.GetInRangeAsync("cal-local", From, To, Arg.Any<CancellationToken>())
            .Returns(new[] { master, exception });

        _events.GetExceptionsAsync("master-1", From, To, Arg.Any<CancellationToken>())
            .Returns(new[] { exception });

        var service = new EventQueryService(_events, _expander);
        var occurrences = await service.GetOccurrencesAsync(From, To, "cal-local");

        occurrences.Should().HaveCount(5);
        occurrences.Single(o => o.IsException).EventId.Should().Be("child-1");
        occurrences.Select(o => o.StartUtc).Should().OnlyHaveUniqueItems();
    }
    [Fact]
    public async Task The_result_of_a_range_is_cached()
    {
        _events.GetInRangeAsync("cal-local", From, To, Arg.Any<CancellationToken>())
            .Returns(new[] { PlainEvent("one") });

        var service = new EventQueryService(_events, _expander);

        await service.GetOccurrencesAsync(From, To, "cal-local");
        await service.GetOccurrencesAsync(From, To, "cal-local");

        await _events.Received(1).GetInRangeAsync("cal-local", From, To, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_different_range_is_cached_separately()
    {
        _events.GetInRangeAsync(Arg.Any<string?>(), Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(new[] { PlainEvent("one") });

        var service = new EventQueryService(_events, _expander);

        await service.GetOccurrencesAsync(From, To, "cal-local");
        await service.GetOccurrencesAsync(From, To, "cal-other");

        await _events.Received(2).GetInRangeAsync(
            Arg.Any<string?>(),
            Arg.Any<DateTimeOffset>(),
            Arg.Any<DateTimeOffset>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Invalidating_the_cache_forces_a_new_query()
    {
        _events.GetInRangeAsync("cal-local", From, To, Arg.Any<CancellationToken>())
            .Returns(new[] { PlainEvent("one") });

        var service = new EventQueryService(_events, _expander);

        await service.GetOccurrencesAsync(From, To, "cal-local");
        service.Invalidate();
        await service.GetOccurrencesAsync(From, To, "cal-local");

        await _events.Received(2).GetInRangeAsync("cal-local", From, To, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Writing_an_event_invalidates_the_query_cache()
    {
        _events.GetInRangeAsync("cal-local", From, To, Arg.Any<CancellationToken>())
            .Returns(new[] { PlainEvent("one") });

        var service = new EventQueryService(_events, _expander);
        var eventService = new EventService(_events, _outbox, _clock, queryCache: service);

        await service.GetOccurrencesAsync(From, To, "cal-local");

        await eventService.CreateAsync(new EventDraft
        {
            CalendarId = "cal-local",
            Title = "New event",
            StartUtc = new DateTimeOffset(2026, 1, 20, 9, 0, 0, TimeSpan.Zero),
            EndUtc = new DateTimeOffset(2026, 1, 20, 10, 0, 0, TimeSpan.Zero),
            TimeZoneId = "UTC",
        });

        await service.GetOccurrencesAsync(From, To, "cal-local");

        await _events.Received(2).GetInRangeAsync("cal-local", From, To, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task The_calendar_filter_is_passed_to_the_repository()
    {
        _events.GetInRangeAsync(Arg.Any<string?>(), Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<CalendarEvent>());

        var service = new EventQueryService(_events, _expander);
        await service.GetOccurrencesAsync(From, To, "cal-work");

        await _events.Received(1).GetInRangeAsync("cal-work", From, To, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task An_event_outside_the_range_is_not_returned()
    {
        var outside = PlainEvent("outside", 10) with
        {
            StartUtc = new DateTimeOffset(2026, 2, 5, 9, 0, 0, TimeSpan.Zero),
            EndUtc = new DateTimeOffset(2026, 2, 5, 10, 0, 0, TimeSpan.Zero),
        };

        _events.GetInRangeAsync(Arg.Any<string?>(), Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(new[] { outside });

        var service = new EventQueryService(_events, _expander);
        var occurrences = await service.GetOccurrencesAsync(From, To, "cal-local");

        occurrences.Should().BeEmpty();
    }
}
