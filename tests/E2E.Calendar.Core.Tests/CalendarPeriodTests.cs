using E2E.Calendar.Core.Models;
using FluentAssertions;
using Xunit;

namespace E2E.Calendar.Core.Tests;

public sealed class CalendarPeriodTests
{
    private static readonly DateOnly Wednesday = new(2026, 10, 21);

    [Fact]
    public void A_day_period_covers_exactly_that_local_day()
    {
        var period = CalendarPeriod.For(CalendarViewMode.Day, Wednesday, "UTC");

        period.FromUtc.Should().Be(new DateTimeOffset(2026, 10, 21, 0, 0, 0, TimeSpan.Zero));
        period.ToUtc.Should().Be(new DateTimeOffset(2026, 10, 22, 0, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public void A_week_period_starts_on_monday_and_ends_on_sunday()
    {
        var period = CalendarPeriod.For(CalendarViewMode.Week, Wednesday, "UTC");

        period.FromUtc.Should().Be(new DateTimeOffset(2026, 10, 19, 0, 0, 0, TimeSpan.Zero));
        period.ToUtc.Should().Be(new DateTimeOffset(2026, 10, 26, 0, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public void A_week_period_for_a_sunday_still_starts_on_the_previous_monday()
    {
        var sunday = new DateOnly(2026, 10, 25);

        var period = CalendarPeriod.For(CalendarViewMode.Week, sunday, "UTC");

        period.FromUtc.Should().Be(new DateTimeOffset(2026, 10, 19, 0, 0, 0, TimeSpan.Zero));
        period.ToUtc.Should().Be(new DateTimeOffset(2026, 10, 26, 0, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public void A_month_period_covers_the_whole_month()
    {
        var period = CalendarPeriod.For(CalendarViewMode.Month, Wednesday, "UTC");

        period.FromUtc.Should().Be(new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero));
        period.ToUtc.Should().Be(new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public void A_month_period_for_december_rolls_into_the_next_year()
    {
        var period = CalendarPeriod.For(CalendarViewMode.Month, new DateOnly(2026, 12, 15), "UTC");

        period.FromUtc.Should().Be(new DateTimeOffset(2026, 12, 1, 0, 0, 0, TimeSpan.Zero));
        period.ToUtc.Should().Be(new DateTimeOffset(2027, 1, 1, 0, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public void An_agenda_period_looks_a_fixed_number_of_days_ahead()
    {
        var period = CalendarPeriod.For(CalendarViewMode.Agenda, Wednesday, "UTC");

        period.FromUtc.Should().Be(new DateTimeOffset(2026, 10, 21, 0, 0, 0, TimeSpan.Zero));
        period.ToUtc.Should().Be(new DateTimeOffset(2026, 10, 21, 0, 0, 0, TimeSpan.Zero).AddDays(CalendarPeriod.AgendaDays));
    }

    [Fact]
    public void A_period_uses_the_offset_of_the_local_day_it_starts_on()
    {
        var beforeDst = CalendarPeriod.For(CalendarViewMode.Day, new DateOnly(2026, 3, 8), "America/New_York");
        var afterDst = CalendarPeriod.For(CalendarViewMode.Day, new DateOnly(2026, 3, 9), "America/New_York");

        beforeDst.FromUtc.Should().Be(new DateTimeOffset(2026, 3, 8, 5, 0, 0, TimeSpan.Zero));
        afterDst.FromUtc.Should().Be(new DateTimeOffset(2026, 3, 9, 4, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public void A_period_falls_back_to_utc_for_an_unknown_time_zone()
    {
        var period = CalendarPeriod.For(CalendarViewMode.Day, Wednesday, "Not/AZone");

        period.FromUtc.Should().Be(new DateTimeOffset(2026, 10, 21, 0, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public void The_start_of_a_week_is_monday()
    {
        CalendarPeriod.StartOfWeek(new DateOnly(2026, 10, 19)).Should().Be(new DateOnly(2026, 10, 19));
        CalendarPeriod.StartOfWeek(new DateOnly(2026, 10, 21)).Should().Be(new DateOnly(2026, 10, 19));
        CalendarPeriod.StartOfWeek(new DateOnly(2026, 10, 25)).Should().Be(new DateOnly(2026, 10, 19));
    }

    [Fact]
    public void The_end_of_a_month_is_the_last_day_of_that_month()
    {
        CalendarPeriod.EndOfMonth(new DateOnly(2026, 10, 21)).Should().Be(new DateOnly(2026, 10, 31));
        CalendarPeriod.EndOfMonth(new DateOnly(2026, 2, 5)).Should().Be(new DateOnly(2026, 2, 28));
        CalendarPeriod.EndOfMonth(new DateOnly(2028, 2, 5)).Should().Be(new DateOnly(2028, 2, 29));
    }

    [Theory]
    [InlineData(CalendarViewMode.Day, "Wed 21 Oct 2026")]
    [InlineData(CalendarViewMode.Month, "October 2026")]
    [InlineData(CalendarViewMode.Agenda, "21 Oct – 19 Nov 2026")]
    public void The_period_label_describes_the_visible_period(CalendarViewMode mode, string expected)
    {
        CalendarPeriod.FormatLabel(mode, Wednesday, "en-US").Should().Be(expected);
    }

    [Fact]
    public void The_week_label_spans_the_first_and_the_last_day()
    {
        var label = CalendarPeriod.FormatLabel(CalendarViewMode.Week, Wednesday, "en-US");

        label.Should().StartWith("19");
        label.Should().EndWith("25 Oct 2026");
    }
}
