using E2E.Calendar.Core.Abstractions;
using E2E.Calendar.Core.Models;
using E2E.Calendar.Core.Services;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace E2E.Calendar.Core.Tests;

public sealed class DailyDigestServiceTests
{
    private readonly IEventRepository _events = Substitute.For<IEventRepository>();
    private readonly IReminderRepository _reminders = Substitute.For<IReminderRepository>();
    private readonly IClock _clock = Substitute.For<IClock>();

    public DailyDigestServiceTests()
    {
        _clock.LocalTimeZone.Returns(TimeZoneInfo.Utc);
        _clock.UtcNow.Returns(At(2026, 11, 1, 8));
    }

    private static DateTimeOffset At(int year, int month, int day, int hour, int minute = 0) =>
        new(year, month, day, hour, minute, 0, TimeSpan.Zero);

    private DailyDigestService CreateService() =>
        new(new EventQueryService(_events, Substitute.For<IRecurrenceExpander>()), _reminders, _clock);

    private static CalendarEvent Event(string id, DateTimeOffset start, DateTimeOffset end) =>
        new()
        {
            Id = id,
            CalendarId = "cal-personal",
            Title = id,
            StartUtc = start,
            EndUtc = end,
            TimeZoneId = "UTC",
            CreatedUtc = At(2026, 1, 1, 0),
            UpdatedUtc = At(2026, 1, 1, 0),
        };

    private static Reminder Reminder(
        string id,
        DateTimeOffset? due,
        DateTimeOffset? done = null,
        bool deleted = false) =>
        new()
        {
            Id = id,
            Title = id,
            DueUtc = due,
            TimeZoneId = "UTC",
            DoneUtc = done,
            Deleted = deleted,
            CreatedUtc = At(2026, 1, 1, 0),
            UpdatedUtc = At(2026, 1, 1, 0),
        };

    [Fact]
    public async Task Today_counts_events_and_only_open_reminders()
    {
        _events
            .GetInRangeAsync(
                Arg.Any<string?>(),
                Arg.Any<DateTimeOffset>(),
                Arg.Any<DateTimeOffset>(),
                Arg.Any<CancellationToken>())
            .Returns(new[]
            {
                Event("morning", At(2026, 11, 1, 8), At(2026, 11, 1, 9)),
                Event("evening", At(2026, 11, 1, 20), At(2026, 11, 1, 21)),
                Event("tomorrow", At(2026, 11, 2, 8), At(2026, 11, 2, 9)),
            });

        _reminders
            .GetInRangeAsync(Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(new[]
            {
                Reminder("open", At(2026, 11, 1, 10)),
                Reminder("done", At(2026, 11, 1, 11), done: At(2026, 11, 1, 11, 30)),
                Reminder("deleted", At(2026, 11, 1, 12), deleted: true),
                Reminder("tomorrow", At(2026, 11, 2, 9)),
            });

        var digest = await CreateService().BuildAsync(At(2026, 11, 1, 8));

        digest.EventCount.Should().Be(2);
        digest.ReminderCount.Should().Be(1);
        digest.Title.Should().Be("Today: 2 events, 1 reminder");
    }

    [Fact]
    public async Task The_day_range_follows_the_local_time_zone()
    {
        var zone = TimeZoneInfo.CreateCustomTimeZone("Test+2", TimeSpan.FromHours(2), "Test+2", "Test+2");
        _clock.LocalTimeZone.Returns(zone);
        _clock.UtcNow.Returns(At(2026, 11, 1, 6));

        DateTimeOffset? fromUtc = null;
        DateTimeOffset? toUtc = null;

        _events
            .GetInRangeAsync(
                Arg.Any<string?>(),
                Arg.Do<DateTimeOffset>(value => fromUtc = value),
                Arg.Do<DateTimeOffset>(value => toUtc = value),
                Arg.Any<CancellationToken>())
            .Returns(Array.Empty<CalendarEvent>());
        _reminders
            .GetInRangeAsync(Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<Reminder>());

        await CreateService().BuildAsync(At(2026, 11, 1, 6));

        fromUtc.Should().Be(new DateTimeOffset(2026, 10, 31, 22, 0, 0, TimeSpan.Zero));
        toUtc.Should().Be(new DateTimeOffset(2026, 11, 1, 22, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public void The_digest_is_due_from_the_chosen_time_until_the_catch_up_window_closes()
    {
        var service = CreateService();
        var eight = new TimeSpan(8, 0, 0);

        service.IsDue(At(2026, 11, 1, 7, 59), eight, null).Should().BeFalse();
        service.IsDue(At(2026, 11, 1, 8), eight, null).Should().BeTrue();
        service.IsDue(At(2026, 11, 1, 11, 59), eight, null).Should().BeTrue();
        service.IsDue(At(2026, 11, 1, 12), eight, null).Should().BeFalse();
    }

    [Fact]
    public void An_already_sent_digest_is_not_due_again_today()
    {
        var service = CreateService();

        service.IsDue(At(2026, 11, 1, 8), new TimeSpan(8, 0, 0), new DateOnly(2026, 11, 1)).Should().BeFalse();
        service.IsDue(At(2026, 11, 1, 8), new TimeSpan(8, 0, 0), new DateOnly(2026, 10, 31)).Should().BeTrue();
    }

    [Fact]
    public void The_digest_time_is_interpreted_in_local_time()
    {
        var zone = TimeZoneInfo.CreateCustomTimeZone("Test+2", TimeSpan.FromHours(2), "Test+2", "Test+2");
        _clock.LocalTimeZone.Returns(zone);

        var service = CreateService();
        var eight = new TimeSpan(8, 0, 0);

        // 05:59 UTC is 07:59 local; 06:00 UTC is 08:00 local.
        service.IsDue(At(2026, 11, 1, 5, 59), eight, null).Should().BeFalse();
        service.IsDue(At(2026, 11, 1, 6), eight, null).Should().BeTrue();
    }
}
