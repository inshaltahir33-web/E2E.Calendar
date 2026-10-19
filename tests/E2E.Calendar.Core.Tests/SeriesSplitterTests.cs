using E2E.Calendar.Core.Models;
using E2E.Calendar.Core.Recurrence;
using FluentAssertions;
using Xunit;

namespace E2E.Calendar.Core.Tests;

public sealed class SeriesSplitterTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 20, 9, 0, 0, TimeSpan.Zero);

    private static CalendarEvent Master(
        string rRule = "FREQ=WEEKLY;BYDAY=MO;COUNT=10",
        string timeZoneId = "UTC",
        DateTimeOffset? startUtc = null) =>
        new()
        {
            Id = "master-1",
            CalendarId = "cal-local",
            RemoteId = "remote-1",
            ETag = "etag-1",
            Title = "Standup",
            StartUtc = startUtc ?? new DateTimeOffset(2026, 1, 5, 9, 0, 0, TimeSpan.Zero),
            EndUtc = (startUtc ?? new DateTimeOffset(2026, 1, 5, 9, 0, 0, TimeSpan.Zero)).AddMinutes(30),
            TimeZoneId = timeZoneId,
            RRule = rRule,
            CreatedUtc = Now.AddDays(-30),
            UpdatedUtc = Now.AddDays(-30),
        };

    [Fact]
    public void Splitting_sets_an_until_on_the_previous_series()
    {
        var split = SeriesSplitter.SplitSeries(
            Master(),
            new DateTimeOffset(2026, 1, 19, 9, 0, 0, TimeSpan.Zero),
            "FREQ=WEEKLY;BYDAY=MO",
            nowUtc: Now);

        split.Previous.RRule.Should().Be("FREQ=WEEKLY;BYDAY=MO;COUNT=10;UNTIL=20260119T085959");
    }

    [Fact]
    public void Splitting_replaces_an_existing_until()
    {
        var split = SeriesSplitter.SplitSeries(
            Master("FREQ=WEEKLY;BYDAY=MO;UNTIL=20260601T090000"),
            new DateTimeOffset(2026, 1, 19, 9, 0, 0, TimeSpan.Zero),
            "FREQ=WEEKLY;BYDAY=MO",
            nowUtc: Now);

        split.Previous.RRule.Should().Be("FREQ=WEEKLY;BYDAY=MO;UNTIL=20260119T085959");
    }

    [Fact]
    public void Splitting_creates_a_new_series_starting_at_the_chosen_occurrence()
    {
        var split = SeriesSplitter.SplitSeries(
            Master(),
            new DateTimeOffset(2026, 1, 19, 9, 0, 0, TimeSpan.Zero),
            "FREQ=WEEKLY;BYDAY=MO",
            nowUtc: Now);

        split.Following.Id.Should().NotBe("master-1");
        split.Following.RRule.Should().Be("FREQ=WEEKLY;BYDAY=MO");
        split.Following.StartUtc.Should().Be(new DateTimeOffset(2026, 1, 19, 9, 0, 0, TimeSpan.Zero));
        split.Following.EndUtc.Should().Be(new DateTimeOffset(2026, 1, 19, 9, 30, 0, TimeSpan.Zero));
    }

    [Fact]
    public void Splitting_keeps_the_duration_of_the_original_event()
    {
        var master = Master() with
        {
            StartUtc = new DateTimeOffset(2026, 1, 5, 9, 0, 0, TimeSpan.Zero),
            EndUtc = new DateTimeOffset(2026, 1, 5, 10, 45, 0, TimeSpan.Zero),
        };

        var split = SeriesSplitter.SplitSeries(
            master,
            new DateTimeOffset(2026, 1, 19, 9, 0, 0, TimeSpan.Zero),
            "FREQ=WEEKLY;BYDAY=MO",
            nowUtc: Now);

        (split.Following.EndUtc - split.Following.StartUtc).Should().Be(TimeSpan.FromMinutes(105));
    }

    [Fact]
    public void Splitting_clears_the_server_side_identity_of_the_new_series()
    {
        var split = SeriesSplitter.SplitSeries(
            Master(),
            new DateTimeOffset(2026, 1, 19, 9, 0, 0, TimeSpan.Zero),
            "FREQ=WEEKLY;BYDAY=MO",
            nowUtc: Now);

        split.Following.RemoteId.Should().BeNull();
        split.Following.ETag.Should().BeNull();
        split.Following.RecurrenceMasterId.Should().BeNull();
        split.Following.RecurrenceIdUtc.Should().BeNull();
        split.Following.Dirty.Should().BeTrue();
        split.Following.Deleted.Should().BeFalse();
    }

    [Fact]
    public void Splitting_keeps_the_title_unless_a_new_one_is_given()
    {
        var split = SeriesSplitter.SplitSeries(
            Master(),
            new DateTimeOffset(2026, 1, 19, 9, 0, 0, TimeSpan.Zero),
            "FREQ=WEEKLY;BYDAY=MO",
            nowUtc: Now);

        split.Following.Title.Should().Be("Standup");

        var renamed = SeriesSplitter.SplitSeries(
            Master(),
            new DateTimeOffset(2026, 1, 19, 9, 0, 0, TimeSpan.Zero),
            "FREQ=WEEKLY;BYDAY=MO",
            newTitle: "Standup (new times)",
            nowUtc: Now);

        renamed.Following.Title.Should().Be("Standup (new times)");
    }

    [Fact]
    public void Splitting_expresses_the_until_in_the_local_time_of_the_event()
    {
        var berlinOccurrence = new DateTimeOffset(2026, 1, 19, 8, 0, 0, TimeSpan.Zero);

        var split = SeriesSplitter.SplitSeries(
            Master(timeZoneId: "Europe/Berlin"),
            berlinOccurrence,
            "FREQ=WEEKLY;BYDAY=MO",
            nowUtc: Now);

        split.Previous.RRule.Should().Be("FREQ=WEEKLY;BYDAY=MO;COUNT=10;UNTIL=20260119T085959");
    }

    [Fact]
    public void The_previous_series_keeps_its_own_identity()
    {
        var split = SeriesSplitter.SplitSeries(
            Master(),
            new DateTimeOffset(2026, 1, 19, 9, 0, 0, TimeSpan.Zero),
            "FREQ=WEEKLY;BYDAY=MO",
            nowUtc: Now);

        split.Previous.Id.Should().Be("master-1");
        split.Previous.RemoteId.Should().Be("remote-1");
        split.Previous.ETag.Should().Be("etag-1");
    }
}
