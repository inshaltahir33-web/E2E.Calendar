using E2E.Calendar.Core.Models;
using E2E.Calendar.Core.Recurrence;
using FluentAssertions;
using Xunit;

namespace E2E.Calendar.Core.Tests;

public sealed class RecurrenceExpanderTests
{
    private readonly RecurrenceExpander _expander = new();

    private static DateTimeOffset Utc(int year, int month, int day, int hour = 9, int minute = 0) =>
        new(year, month, day, hour, minute, 0, TimeSpan.Zero);

    private static CalendarEvent Master(
        string? rRule,
        DateTimeOffset? startUtc = null,
        DateTimeOffset? endUtc = null,
        string timeZoneId = "UTC",
        string? exDates = null) =>
        new()
        {
            Id = "master-1",
            CalendarId = "cal-local",
            Title = "Standup",
            StartUtc = startUtc ?? Utc(2026, 1, 5),
            EndUtc = endUtc ?? (startUtc ?? Utc(2026, 1, 5)).AddMinutes(30),
            TimeZoneId = timeZoneId,
            RRule = rRule,
            ExDates = exDates,
            CreatedUtc = Utc(2025, 12, 1),
            UpdatedUtc = Utc(2025, 12, 1),
        };

    private static CalendarEvent Exception(
        string id,
        DateTimeOffset originalStartUtc,
        DateTimeOffset? newStartUtc = null,
        DateTimeOffset? newEndUtc = null) =>
        new()
        {
            Id = id,
            CalendarId = "cal-local",
            Title = "Standup (moved)",
            StartUtc = newStartUtc ?? originalStartUtc,
            EndUtc = newEndUtc ?? (newStartUtc ?? originalStartUtc).AddMinutes(30),
            TimeZoneId = "UTC",
            RecurrenceMasterId = "master-1",
            RecurrenceIdUtc = originalStartUtc,
            CreatedUtc = Utc(2025, 12, 1),
            UpdatedUtc = Utc(2025, 12, 1),
        };

    private IReadOnlyList<Occurrence> Expand(
        CalendarEvent master,
        IEnumerable<CalendarEvent>? exceptions = null,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null) =>
        _expander
            .Expand(
                master,
                exceptions ?? Array.Empty<CalendarEvent>(),
                from ?? Utc(2020, 1, 1),
                to ?? Utc(2035, 1, 1))
            .ToList();

    [Fact]
    public void A_daily_series_expands_once_per_day()
    {
        var occurrences = Expand(Master("FREQ=DAILY;COUNT=5"));

        occurrences.Should().HaveCount(5);
        occurrences.Select(o => o.StartUtc).Should().Equal(
            Utc(2026, 1, 5),
            Utc(2026, 1, 6),
            Utc(2026, 1, 7),
            Utc(2026, 1, 8),
            Utc(2026, 1, 9));
    }

    [Fact]
    public void A_daily_series_honours_the_interval()
    {
        var occurrences = Expand(Master("FREQ=DAILY;INTERVAL=3;COUNT=4"));

        occurrences.Select(o => o.StartUtc).Should().Equal(
            Utc(2026, 1, 5),
            Utc(2026, 1, 8),
            Utc(2026, 1, 11),
            Utc(2026, 1, 14));
    }

    [Fact]
    public void A_weekly_series_expands_on_the_requested_week_days()
    {
        var occurrences = Expand(Master("FREQ=WEEKLY;BYDAY=MO,WE;COUNT=6"));

        occurrences.Should().HaveCount(6);
        occurrences.Select(o => o.StartUtc).Should().Equal(
            Utc(2026, 1, 5),
            Utc(2026, 1, 7),
            Utc(2026, 1, 12),
            Utc(2026, 1, 14),
            Utc(2026, 1, 19),
            Utc(2026, 1, 21));
        occurrences.Should().AllSatisfy(
            o => o.StartUtc.DayOfWeek.Should().BeOneOf(DayOfWeek.Monday, DayOfWeek.Wednesday));
    }

    [Fact]
    public void A_weekly_series_honours_a_multi_week_interval()
    {
        var occurrences = Expand(Master("FREQ=WEEKLY;INTERVAL=2;BYDAY=MO;COUNT=3"));

        occurrences.Select(o => o.StartUtc).Should().Equal(
            Utc(2026, 1, 5),
            Utc(2026, 1, 19),
            Utc(2026, 2, 2));
    }

    [Fact]
    public void A_monthly_series_by_date_expands_once_per_month()
    {
        var occurrences = Expand(Master("FREQ=MONTHLY;BYMONTHDAY=15;COUNT=4"));

        occurrences.Select(o => o.StartUtc).Should().Equal(
            Utc(2026, 1, 15),
            Utc(2026, 2, 15),
            Utc(2026, 3, 15),
            Utc(2026, 4, 15));
    }

    [Fact]
    public void A_monthly_series_by_nth_weekday_expands_the_second_tuesday()
    {
        var occurrences = Expand(Master(
            "FREQ=MONTHLY;BYDAY=2TU;COUNT=4",
            Utc(2026, 1, 13)));

        occurrences.Select(o => o.StartUtc).Should().Equal(
            Utc(2026, 1, 13),
            Utc(2026, 2, 10),
            Utc(2026, 3, 10),
            Utc(2026, 4, 14));
        occurrences.Should().OnlyContain(o => o.StartUtc.DayOfWeek == DayOfWeek.Tuesday);
    }

    [Fact]
    public void A_monthly_series_on_the_thirty_first_skips_months_without_one()
    {
        var occurrences = Expand(Master(
            "FREQ=MONTHLY;COUNT=4",
            Utc(2026, 1, 31)));

        occurrences.Select(o => o.StartUtc).Should().Equal(
            Utc(2026, 1, 31),
            Utc(2026, 3, 31),
            Utc(2026, 5, 31),
            Utc(2026, 7, 31));
    }

    [Fact]
    public void A_yearly_series_expands_once_per_year()
    {
        var occurrences = Expand(Master("FREQ=YEARLY;COUNT=3", Utc(2026, 6, 1)));

        occurrences.Select(o => o.StartUtc).Should().Equal(
            Utc(2026, 6, 1),
            Utc(2027, 6, 1),
            Utc(2028, 6, 1));
    }

    [Fact]
    public void A_leap_day_series_only_expands_in_leap_years()
    {
        var occurrences = Expand(Master(
            "FREQ=YEARLY;BYMONTH=2;BYMONTHDAY=29;COUNT=2",
            Utc(2026, 2, 28)));

        occurrences.Select(o => o.StartUtc).Should().Equal(
            Utc(2028, 2, 29),
            Utc(2032, 2, 29));
    }

    [Fact]
    public void A_series_with_a_count_stops_after_that_many_occurrences()
    {
        var occurrences = Expand(Master("FREQ=DAILY;COUNT=3"));

        occurrences.Should().HaveCount(3);
    }

    [Fact]
    public void A_series_with_an_until_stops_at_that_moment()
    {
        var occurrences = Expand(Master("FREQ=DAILY;UNTIL=20260108T090000Z"));

        occurrences.Select(o => o.StartUtc).Should().Equal(
            Utc(2026, 1, 5),
            Utc(2026, 1, 6),
            Utc(2026, 1, 7),
            Utc(2026, 1, 8));
    }

    [Fact]
    public void An_exdate_removes_a_single_occurrence()
    {
        var master = Master(
            "FREQ=DAILY;COUNT=5",
            exDates: ExDateList.Format(new[] { Utc(2026, 1, 7) }));

        var occurrences = Expand(master);

        occurrences.Should().HaveCount(4);
        occurrences.Select(o => o.StartUtc).Should().NotContain(Utc(2026, 1, 7));
    }

    [Fact]
    public void Several_exdates_remove_several_occurrences()
    {
        var master = Master(
            "FREQ=DAILY;COUNT=6",
            exDates: ExDateList.Format(new[] { Utc(2026, 1, 6), Utc(2026, 1, 9) }));

        var occurrences = Expand(master);

        occurrences.Should().HaveCount(4);
        occurrences.Select(o => o.StartUtc).Should().NotContain(Utc(2026, 1, 6));
        occurrences.Select(o => o.StartUtc).Should().NotContain(Utc(2026, 1, 9));
    }

    [Fact]
    public void An_exception_replaces_the_occurrence_it_refers_to()
    {
        var exception = Exception("child-1", Utc(2026, 1, 7), Utc(2026, 1, 7, 11), Utc(2026, 1, 7, 12));

        var occurrences = Expand(Master("FREQ=DAILY;COUNT=5"), new[] { exception });

        occurrences.Should().HaveCount(5);

        var replaced = occurrences.Single(o => o.IsException);
        replaced.EventId.Should().Be("child-1");
        replaced.StartUtc.Should().Be(Utc(2026, 1, 7, 11));
        replaced.EndUtc.Should().Be(Utc(2026, 1, 7, 12));

        occurrences.Count(o => !o.IsException).Should().Be(4);
    }

    [Fact]
    public void An_exception_can_move_an_occurrence_to_another_day()
    {
        var exception = Exception("child-1", Utc(2026, 1, 7), Utc(2026, 1, 12, 9), Utc(2026, 1, 12, 9, 30));

        var occurrences = Expand(Master("FREQ=DAILY;COUNT=5"), new[] { exception });

        occurrences.Should().HaveCount(5);
        occurrences.Single(o => o.IsException).StartUtc.Should().Be(Utc(2026, 1, 12, 9));
    }

    [Fact]
    public void Occurrences_outside_the_requested_range_are_not_returned()
    {
        var occurrences = Expand(
            Master("FREQ=DAILY;COUNT=30"),
            from: Utc(2026, 1, 10),
            to: Utc(2026, 1, 15));

        occurrences.Select(o => o.StartUtc).Should().Equal(
            Utc(2026, 1, 10),
            Utc(2026, 1, 11),
            Utc(2026, 1, 12),
            Utc(2026, 1, 13),
            Utc(2026, 1, 14));
    }

    [Fact]
    public void An_event_without_a_rule_has_no_occurrences()
    {
        Expand(Master(null)).Should().BeEmpty();
    }

    [Fact]
    public void Every_occurrence_keeps_the_duration_of_the_master()
    {
        var occurrences = Expand(Master("FREQ=DAILY;COUNT=3", Utc(2026, 1, 5, 9), Utc(2026, 1, 5, 10, 30)));

        occurrences.Should().OnlyContain(o => o.EndUtc - o.StartUtc == TimeSpan.FromMinutes(90));
    }

    [Fact]
    public void A_daily_series_keeps_its_local_wall_clock_time_across_a_dst_spring_forward()
    {
        var berlinStart = new DateTimeOffset(2026, 3, 27, 8, 0, 0, TimeSpan.Zero);

        var occurrences = Expand(Master(
            "FREQ=DAILY;COUNT=6",
            berlinStart,
            berlinStart.AddHours(1),
            "Europe/Berlin"));

        occurrences.Should().HaveCount(6);

        foreach (var occurrence in occurrences)
        {
            var local = TimeZoneInfo.ConvertTimeFromUtc(occurrence.StartUtc.UtcDateTime, TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin"));

            local.Hour.Should().Be(9);
            local.Minute.Should().Be(0);
        }

        occurrences[0].StartUtc.Should().Be(new DateTimeOffset(2026, 3, 27, 8, 0, 0, TimeSpan.Zero));
        occurrences[3].StartUtc.Should().Be(new DateTimeOffset(2026, 3, 30, 7, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public void A_daily_series_keeps_its_local_wall_clock_time_across_a_dst_fall_back()
    {
        var newYorkStart = new DateTimeOffset(2026, 10, 30, 13, 0, 0, TimeSpan.Zero);

        var occurrences = Expand(Master(
            "FREQ=DAILY;COUNT=5",
            newYorkStart,
            newYorkStart.AddHours(1),
            "America/New_York"));

        occurrences.Should().HaveCount(5);

        foreach (var occurrence in occurrences)
        {
            var local = TimeZoneInfo.ConvertTimeFromUtc(occurrence.StartUtc.UtcDateTime, TimeZoneInfo.FindSystemTimeZoneById("America/New_York"));

            local.Hour.Should().Be(9);
        }

        occurrences[0].StartUtc.Should().Be(new DateTimeOffset(2026, 10, 30, 13, 0, 0, TimeSpan.Zero));
        occurrences[2].StartUtc.Should().Be(new DateTimeOffset(2026, 11, 1, 14, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public void A_weekly_series_keeps_its_local_wall_clock_time_across_a_dst_spring_forward()
    {
        var berlinStart = new DateTimeOffset(2026, 3, 23, 8, 0, 0, TimeSpan.Zero);

        var occurrences = Expand(Master(
            "FREQ=WEEKLY;BYDAY=MO;COUNT=4",
            berlinStart,
            berlinStart.AddHours(1),
            "Europe/Berlin"));

        occurrences.Should().HaveCount(4);

        foreach (var occurrence in occurrences)
        {
            var local = TimeZoneInfo.ConvertTimeFromUtc(occurrence.StartUtc.UtcDateTime, TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin"));

            local.Hour.Should().Be(9);
        }
    }

    [Fact]
    public void An_occurrence_inside_a_dst_gap_is_moved_past_the_gap()
    {
        var berlin = TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");
        var local = new DateTime(2026, 3, 28, 2, 30, 0, DateTimeKind.Unspecified);

        var startUtc = new DateTimeOffset(local, berlin.GetUtcOffset(local)).ToUniversalTime();

        var occurrences = Expand(Master(
            "FREQ=DAILY;COUNT=2",
            startUtc,
            startUtc.AddHours(1),
            "Europe/Berlin"));

        occurrences.Should().HaveCount(2);
        occurrences[0].StartUtc.Should().Be(new DateTimeOffset(2026, 3, 28, 1, 30, 0, TimeSpan.Zero));
        occurrences[1].StartUtc.Should().Be(new DateTimeOffset(2026, 3, 29, 1, 30, 0, TimeSpan.Zero));

        var localOccurrence = TimeZoneInfo.ConvertTimeFromUtc(occurrences[1].StartUtc.UtcDateTime, berlin);
        localOccurrence.Hour.Should().Be(3);
    }

    [Fact]
    public void A_series_in_an_unknown_time_zone_is_treated_as_utc()
    {
        var occurrences = Expand(Master("FREQ=DAILY;COUNT=2", Utc(2026, 1, 5), Utc(2026, 1, 5, 10), "Not/AZone"));

        occurrences.Select(o => o.StartUtc).Should().Equal(Utc(2026, 1, 5), Utc(2026, 1, 6));
    }

    [Fact]
    public void An_occurrence_that_ends_after_the_range_start_is_still_returned()
    {
        var occurrences = Expand(
            Master("FREQ=DAILY;COUNT=3", Utc(2026, 1, 5, 9), Utc(2026, 1, 5, 12)),
            from: Utc(2026, 1, 5, 10),
            to: Utc(2026, 1, 5, 11));

        occurrences.Should().HaveCount(1);
        occurrences[0].StartUtc.Should().Be(Utc(2026, 1, 5, 9));
    }
}
