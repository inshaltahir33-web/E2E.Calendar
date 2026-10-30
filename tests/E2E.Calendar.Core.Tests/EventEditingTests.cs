using E2E.Calendar.Core.Abstractions;
using E2E.Calendar.Core.Entitlements;
using E2E.Calendar.Core.Models;
using E2E.Calendar.Core.Services;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace E2E.Calendar.Core.Tests;

public sealed class EventEditingTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 21, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset End = new(2026, 10, 21, 10, 0, 0, TimeSpan.Zero);

    private static CalendarEvent Master() => new()
    {
        Id = "master-1",
        CalendarId = "cal-local",
        Title = "Standup",
        StartUtc = Start,
        EndUtc = End,
        TimeZoneId = "Asia/Kolkata",
        RRule = "FREQ=WEEKLY;BYDAY=MO;COUNT=10",
        CreatedUtc = Start,
        UpdatedUtc = Start,
    };

    private static CalendarEvent Edited() => Master() with { Title = "Renamed" };

    [Fact]
    public void An_event_that_ends_before_it_starts_is_rejected()
    {
        var errors = EventValidator.Validate("Standup", End, Start);

        errors.Should().Contain("The event cannot end before it starts.");
    }

    [Fact]
    public void An_event_without_a_title_is_rejected()
    {
        var errors = EventValidator.Validate("  ", Start, End);

        errors.Should().Contain("The event needs a title.");
    }

    [Fact]
    public void A_link_with_an_unsupported_scheme_is_rejected()
    {
        var errors = EventValidator.Validate("Standup", Start, End, "javascript:alert(1)");

        errors.Should().Contain("The link must be an http or https address.");
    }

    [Fact]
    public void A_valid_event_has_no_errors()
    {
        var errors = EventValidator.Validate("Standup", Start, End, "https://example.invalid/e");

        errors.Should().BeEmpty();
    }

    [Fact]
    public async Task Editing_a_single_occurrence_creates_an_exception()
    {
        var events = Substitute.For<IEventRepository>();
        var outbox = Substitute.For<IOutbox>();
        var clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(Start);

        events.GetExceptionsAsync("master-1", Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<CalendarEvent>());

        var service = new RecurrenceEditService(events, outbox, clock);

        var saved = await service.ApplyAsync(Master(), Start, Edited(), RecurrenceEditScope.ThisEvent);

        saved.Should().HaveCount(1);
        saved[0].RecurrenceMasterId.Should().Be("master-1");
        saved[0].RecurrenceIdUtc.Should().Be(Start);
        saved[0].Title.Should().Be("Renamed");
        saved[0].Dirty.Should().BeTrue();

        await events.Received(1).UpsertAsync(Arg.Any<CalendarEvent>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Editing_this_and_following_splits_the_series()
    {
        var events = Substitute.For<IEventRepository>();
        var outbox = Substitute.For<IOutbox>();
        var clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(Start);

        var service = new RecurrenceEditService(events, outbox, clock);

        var occurrence = new DateTimeOffset(2026, 10, 26, 9, 0, 0, TimeSpan.Zero);
        var saved = await service.ApplyAsync(Master(), occurrence, Edited(), RecurrenceEditScope.ThisAndFollowing);

        saved.Should().HaveCount(2);

        saved[0].RRule.Should().Contain("UNTIL=20261026T142959");
        saved[1].StartUtc.Should().Be(occurrence);
        saved[1].Title.Should().Be("Renamed");
        saved[1].Id.Should().NotBe("master-1");
    }

    [Fact]
    public async Task Editing_all_events_updates_the_master()
    {
        var events = Substitute.For<IEventRepository>();
        var outbox = Substitute.For<IOutbox>();
        var clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(Start);

        var service = new RecurrenceEditService(events, outbox, clock);

        var saved = await service.ApplyAsync(Master(), Start, Edited(), RecurrenceEditScope.AllEvents);

        saved.Should().HaveCount(1);
        saved[0].Id.Should().Be("master-1");
        saved[0].Title.Should().Be("Renamed");
        saved[0].Dirty.Should().BeTrue();
    }
}
