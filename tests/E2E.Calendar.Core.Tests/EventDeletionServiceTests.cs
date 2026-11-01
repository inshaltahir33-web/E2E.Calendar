using E2E.Calendar.Core.Abstractions;
using E2E.Calendar.Core.Models;
using E2E.Calendar.Core.Recurrence;
using E2E.Calendar.Core.Services;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace E2E.Calendar.Core.Tests;

public sealed class EventDeletionServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 5, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Occurrence = new(2026, 1, 7, 9, 0, 0, TimeSpan.Zero);

    private readonly IEventRepository _events = Substitute.For<IEventRepository>();
    private readonly IOutbox _outbox = Substitute.For<IOutbox>();
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly IEventQueryCache _cache = Substitute.For<IEventQueryCache>();
    private readonly List<CalendarEvent> _saved = new();

    public EventDeletionServiceTests()
    {
        _clock.UtcNow.Returns(Now);
        _events
            .UpsertAsync(Arg.Do<CalendarEvent>(saved => _saved.Add(saved)), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
    }

    private EventDeletionService CreateService() => new(_events, _outbox, _clock, _cache);

    private static CalendarEvent Recurring(string? exDates = null) =>
        new()
        {
            Id = "master-1",
            CalendarId = "cal-local",
            Title = "Standup",
            StartUtc = new DateTimeOffset(2026, 1, 5, 9, 0, 0, TimeSpan.Zero),
            EndUtc = new DateTimeOffset(2026, 1, 5, 9, 30, 0, TimeSpan.Zero),
            TimeZoneId = "UTC",
            RRule = "FREQ=DAILY;COUNT=5",
            ExDates = exDates,
            CreatedUtc = Now,
            UpdatedUtc = Now,
        };

    private static CalendarEvent Exception(DateTimeOffset originalStart, DateTimeOffset movedStart) =>
        new()
        {
            Id = "child-1",
            CalendarId = "cal-local",
            Title = "Standup (moved)",
            StartUtc = movedStart,
            EndUtc = movedStart.AddMinutes(30),
            TimeZoneId = "UTC",
            RecurrenceMasterId = "master-1",
            RecurrenceIdUtc = originalStart,
            CreatedUtc = Now,
            UpdatedUtc = Now,
        };

    [Fact]
    public async Task Deleting_a_single_event_soft_deletes_it_and_undo_restores_it()
    {
        var calendarEvent = Recurring() with { Id = "event-1", RRule = null };
        var service = CreateService();

        var deletion = await service.DeleteAsync(
            calendarEvent,
            calendarEvent.StartUtc,
            RecurrenceEditScope.AllEvents);

        _saved.Should().ContainSingle();
        _saved[0].Deleted.Should().BeTrue();
        _cache.Received(1).Invalidate();

        await service.UndoAsync(deletion);

        _saved.Should().HaveCount(2);
        _saved[1].Deleted.Should().BeFalse();
        _saved[1].Id.Should().Be("event-1");
    }

    [Fact]
    public async Task This_event_adds_an_exdate_and_keeps_the_rule()
    {
        var master = Recurring();
        var service = CreateService();

        var deletion = await service.DeleteAsync(master, Occurrence, RecurrenceEditScope.ThisEvent);

        var saved = _saved.Should().ContainSingle().Subject;
        saved.Deleted.Should().BeFalse();
        saved.RRule.Should().Be(master.RRule);
        saved.ExDates.Should().Be(ExDateList.Format(Occurrence));

        await service.UndoAsync(deletion);

        _saved[^1].ExDates.Should().BeNull();
        _saved[^1].RRule.Should().Be(master.RRule);
    }

    [Fact]
    public async Task This_event_keeps_exdates_that_were_already_there()
    {
        var existing = new DateTimeOffset(2026, 1, 6, 9, 0, 0, TimeSpan.Zero);
        var master = Recurring(ExDateList.Format(existing));

        await CreateService().DeleteAsync(master, Occurrence, RecurrenceEditScope.ThisEvent);

        _saved.Single().ExDates.Should().Be(ExDateList.Format(new[] { existing, Occurrence }));
    }

    [Fact]
    public async Task This_and_following_truncates_the_rule_and_undo_restores_it()
    {
        var master = Recurring();
        var service = CreateService();

        var deletion = await service.DeleteAsync(master, Occurrence, RecurrenceEditScope.ThisAndFollowing);

        var saved = _saved.Should().ContainSingle().Subject;
        saved.Deleted.Should().BeFalse();
        saved.RRule.Should().Contain("UNTIL=20260107T085959");
        saved.ExDates.Should().BeNull();

        await service.UndoAsync(deletion);

        _saved[^1].RRule.Should().Be(master.RRule);
        _saved[^1].Deleted.Should().BeFalse();
    }

    [Fact]
    public async Task This_event_on_an_edited_occurrence_retires_the_row_and_excludes_the_occurrence()
    {
        var master = Recurring();
        var exception = Exception(Occurrence, new DateTimeOffset(2026, 1, 12, 9, 0, 0, TimeSpan.Zero));
        _events.GetByIdAsync(master.Id, Arg.Any<CancellationToken>()).Returns(master);
        var service = CreateService();

        var deletion = await service.DeleteAsync(exception, exception.StartUtc, RecurrenceEditScope.ThisEvent);

        _saved.Should().HaveCount(2);

        var retired = _saved.Single(saved => saved.Id == exception.Id);
        retired.Deleted.Should().BeTrue();

        var updatedMaster = _saved.Single(saved => saved.Id == master.Id);
        updatedMaster.Deleted.Should().BeFalse();
        updatedMaster.ExDates.Should().Be(ExDateList.Format(Occurrence));

        await service.UndoAsync(deletion);

        _saved[^2].Deleted.Should().BeFalse();
        _saved[^1].ExDates.Should().BeNull();
    }

    [Fact]
    public async Task All_events_from_an_edited_occurrence_deletes_the_series()
    {
        var master = Recurring();
        var exception = Exception(Occurrence, new DateTimeOffset(2026, 1, 12, 9, 0, 0, TimeSpan.Zero));
        _events.GetByIdAsync(master.Id, Arg.Any<CancellationToken>()).Returns(master);

        await CreateService().DeleteAsync(exception, exception.StartUtc, RecurrenceEditScope.AllEvents);

        _saved.Should().ContainSingle();
        _saved[0].Id.Should().Be(master.Id);
        _saved[0].Deleted.Should().BeTrue();
    }

    [Fact]
    public async Task This_and_following_from_an_edited_occurrence_uses_the_original_occurrence()
    {
        var master = Recurring();
        var exception = Exception(Occurrence, new DateTimeOffset(2026, 1, 12, 9, 0, 0, TimeSpan.Zero));
        _events.GetByIdAsync(master.Id, Arg.Any<CancellationToken>()).Returns(master);

        await CreateService().DeleteAsync(exception, exception.StartUtc, RecurrenceEditScope.ThisAndFollowing);

        var saved = _saved.Should().ContainSingle().Subject;
        saved.Id.Should().Be(master.Id);
        saved.RRule.Should().Contain("UNTIL=20260107T085959");
    }

    [Fact]
    public async Task Undo_after_the_window_has_expired_is_rejected()
    {
        var calendarEvent = Recurring() with { Id = "event-1", RRule = null };
        var service = CreateService();
        var deletion = await service.DeleteAsync(
            calendarEvent,
            calendarEvent.StartUtc,
            RecurrenceEditScope.AllEvents);

        _clock.UtcNow.Returns(Now.AddSeconds(11));

        var act = () => service.UndoAsync(deletion);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}
