using E2E.Calendar.Core.Models;
using E2E.Calendar.Core.Services;
using FluentAssertions;
using Xunit;

namespace E2E.Calendar.Core.Tests;

public sealed class MiniCalendarGridTests
{
    private static readonly DateOnly Today = new(2026, 10, 20);

    [Fact]
    public void A_monday_week_start_begins_the_grid_on_monday()
    {
        var grid = MiniCalendarGridBuilder.Build(2026, 10, WeekStartDay.Monday, Today, null);

        grid.FirstVisibleDay.DayOfWeek.Should().Be(DayOfWeek.Monday);
        grid.FirstVisibleDay.Should().Be(new DateOnly(2026, 9, 28));
    }

    [Fact]
    public void A_sunday_week_start_begins_the_grid_on_sunday()
    {
        var grid = MiniCalendarGridBuilder.Build(2026, 10, WeekStartDay.Sunday, Today, null);

        grid.FirstVisibleDay.DayOfWeek.Should().Be(DayOfWeek.Sunday);
        grid.FirstVisibleDay.Should().Be(new DateOnly(2026, 9, 27));
    }

    [Fact]
    public void A_saturday_week_start_begins_the_grid_on_saturday()
    {
        var grid = MiniCalendarGridBuilder.Build(2026, 10, WeekStartDay.Saturday, Today, null);

        grid.FirstVisibleDay.DayOfWeek.Should().Be(DayOfWeek.Saturday);
        grid.FirstVisibleDay.Should().Be(new DateOnly(2026, 9, 26));
    }

    [Fact]
    public void Every_week_contains_seven_consecutive_days()
    {
        var grid = MiniCalendarGridBuilder.Build(2026, 10, WeekStartDay.Monday, Today, null);

        grid.Weeks.Should().NotBeEmpty();
        grid.Weeks.Should().OnlyContain(week => week.Days.Count == 7);

        foreach (var week in grid.Weeks)
        {
            for (var index = 1; index < week.Days.Count; index++)
            {
                week.Days[index].Date.Should().Be(week.Days[index - 1].Date.AddDays(1));
            }
        }
    }

    [Fact]
    public void A_six_row_month_produces_six_weeks()
    {
        var grid = MiniCalendarGridBuilder.Build(2026, 8, WeekStartDay.Monday, Today, null);

        grid.Weeks.Should().HaveCount(6);
        grid.FirstVisibleDay.Should().Be(new DateOnly(2026, 7, 27));
        grid.LastVisibleDay.Should().Be(new DateOnly(2026, 9, 6));
    }

    [Fact]
    public void A_five_row_month_produces_five_weeks()
    {
        var grid = MiniCalendarGridBuilder.Build(2026, 2, WeekStartDay.Monday, Today, null);

        grid.Weeks.Should().HaveCount(5);
        grid.FirstVisibleDay.Should().Be(new DateOnly(2026, 1, 26));
        grid.LastVisibleDay.Should().Be(new DateOnly(2026, 3, 1));
    }

    [Fact]
    public void A_month_that_fits_exactly_produces_four_weeks()
    {
        var grid = MiniCalendarGridBuilder.Build(2026, 2, WeekStartDay.Sunday, Today, null);

        grid.Weeks.Should().HaveCount(4);
        grid.FirstVisibleDay.Should().Be(new DateOnly(2026, 2, 1));
        grid.LastVisibleDay.Should().Be(new DateOnly(2026, 2, 28));
    }

    [Fact]
    public void Days_outside_the_month_are_marked()
    {
        var grid = MiniCalendarGridBuilder.Build(2026, 10, WeekStartDay.Monday, Today, null);
        var days = grid.Weeks.SelectMany(week => week.Days).ToList();

        days.Count(day => day.IsCurrentMonth).Should().Be(31);
        days.First().IsCurrentMonth.Should().BeFalse();
        days.Last().IsCurrentMonth.Should().BeFalse();
    }

    [Fact]
    public void Today_is_flagged()
    {
        var grid = MiniCalendarGridBuilder.Build(2026, 10, WeekStartDay.Monday, Today, null);

        grid.Weeks.SelectMany(week => week.Days).Count(day => day.IsToday).Should().Be(1);
        grid.Weeks.SelectMany(week => week.Days).Single(day => day.IsToday).Date.Should().Be(Today);
    }

    [Fact]
    public void The_selected_day_and_its_week_are_flagged()
    {
        var selected = new DateOnly(2026, 10, 21);

        var grid = MiniCalendarGridBuilder.Build(2026, 10, WeekStartDay.Monday, Today, selected);
        var days = grid.Weeks.SelectMany(week => week.Days).ToList();

        days.Count(day => day.IsSelected).Should().Be(1);
        days.Single(day => day.IsSelected).Date.Should().Be(selected);
        days.Count(day => day.IsInSelectedWeek).Should().Be(7);
    }

    [Fact]
    public void Event_counts_are_taken_from_the_supplied_map()
    {
        var counts = new Dictionary<DateOnly, int>
        {
            [new DateOnly(2026, 10, 20)] = 3,
            [new DateOnly(2026, 10, 21)] = 1,
        };

        var grid = MiniCalendarGridBuilder.Build(2026, 10, WeekStartDay.Monday, Today, null, counts);
        var days = grid.Weeks.SelectMany(week => week.Days).ToList();

        days.Single(day => day.Date == new DateOnly(2026, 10, 20)).EventCount.Should().Be(3);
        days.Single(day => day.Date == new DateOnly(2026, 10, 21)).EventCount.Should().Be(1);
        days.Single(day => day.Date == new DateOnly(2026, 10, 22)).EventCount.Should().Be(0);
    }

    [Fact]
    public void The_grid_covers_the_whole_month()
    {
        var grid = MiniCalendarGridBuilder.Build(2026, 10, WeekStartDay.Monday, Today, null);
        var days = grid.Weeks.SelectMany(week => week.Days).ToList();

        days.Should().Contain(day => day.Date == new DateOnly(2026, 10, 1));
        days.Should().Contain(day => day.Date == new DateOnly(2026, 10, 31));
    }

    [Fact]
    public void A_february_in_a_leap_year_covers_the_twenty_ninth()
    {
        var grid = MiniCalendarGridBuilder.Build(2028, 2, WeekStartDay.Monday, Today, null);
        var days = grid.Weeks.SelectMany(week => week.Days).ToList();

        days.Should().Contain(day => day.Date == new DateOnly(2028, 2, 29));
    }

    [Fact]
    public void An_invalid_month_is_rejected()
    {
        var act = () => MiniCalendarGridBuilder.Build(2026, 13, WeekStartDay.Monday, Today, null);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
