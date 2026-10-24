using E2E.Calendar.Core.Recurrence;
using FluentAssertions;
using Xunit;

namespace E2E.Calendar.Core.Tests;

public sealed class RecurrenceRuleBuilderTests
{
    [Fact]
    public void No_repeat_produces_no_rule()
    {
        RecurrenceRuleBuilder.Build(new RecurrenceRuleOptions { Frequency = RecurrenceFrequency.None })
            .Should().BeNull();
    }

    [Fact]
    public void A_daily_rule_maps_to_a_daily_frequency()
    {
        RecurrenceRuleBuilder.Build(new RecurrenceRuleOptions { Frequency = RecurrenceFrequency.Daily })
            .Should().Be("FREQ=DAILY");
    }

    [Fact]
    public void The_interval_maps_to_the_interval_part()
    {
        RecurrenceRuleBuilder.Build(new RecurrenceRuleOptions { Frequency = RecurrenceFrequency.Daily, Interval = 3 })
            .Should().Be("FREQ=DAILY;INTERVAL=3");
    }

    [Fact]
    public void Weekly_rules_map_the_selected_days()
    {
        RecurrenceRuleBuilder.Build(new RecurrenceRuleOptions
        {
            Frequency = RecurrenceFrequency.Weekly,
            WeeklyDays = [DayOfWeek.Wednesday, DayOfWeek.Monday],
        }).Should().Be("FREQ=WEEKLY;BYDAY=MO,WE");
    }

    [Fact]
    public void Monthly_rules_can_map_to_a_day_of_the_month()
    {
        RecurrenceRuleBuilder.Build(new RecurrenceRuleOptions
        {
            Frequency = RecurrenceFrequency.Monthly,
            MonthlyPattern = MonthlyPattern.ByDate,
            MonthlyDay = 15,
        }).Should().Be("FREQ=MONTHLY;BYMONTHDAY=15");
    }

    [Fact]
    public void Monthly_rules_can_map_to_the_nth_weekday()
    {
        RecurrenceRuleBuilder.Build(new RecurrenceRuleOptions
        {
            Frequency = RecurrenceFrequency.Monthly,
            MonthlyPattern = MonthlyPattern.ByWeekday,
            MonthlyWeekdayOrdinal = 2,
            MonthlyWeekday = DayOfWeek.Tuesday,
        }).Should().Be("FREQ=MONTHLY;BYDAY=2TU");
    }

    [Fact]
    public void The_last_weekday_of_the_month_uses_a_negative_ordinal()
    {
        RecurrenceRuleBuilder.Build(new RecurrenceRuleOptions
        {
            Frequency = RecurrenceFrequency.Monthly,
            MonthlyPattern = MonthlyPattern.ByWeekday,
            MonthlyWeekdayOrdinal = -1,
            MonthlyWeekday = DayOfWeek.Friday,
        }).Should().Be("FREQ=MONTHLY;BYDAY=-1FR");
    }

    [Fact]
    public void Yearly_rules_map_the_month_and_the_day()
    {
        RecurrenceRuleBuilder.Build(new RecurrenceRuleOptions
        {
            Frequency = RecurrenceFrequency.Yearly,
            YearlyMonth = 3,
            MonthlyDay = 15,
        }).Should().Be("FREQ=YEARLY;BYMONTH=3;BYMONTHDAY=15");
    }

    [Fact]
    public void An_end_date_maps_to_an_until_part()
    {
        RecurrenceRuleBuilder.Build(new RecurrenceRuleOptions
        {
            Frequency = RecurrenceFrequency.Daily,
            End = RecurrenceEnd.OnDate,
            EndDate = new DateOnly(2026, 12, 31),
        }).Should().Be("FREQ=DAILY;UNTIL=20261231T235959");
    }

    [Fact]
    public void An_end_after_n_occurrences_maps_to_a_count_part()
    {
        RecurrenceRuleBuilder.Build(new RecurrenceRuleOptions
        {
            Frequency = RecurrenceFrequency.Weekly,
            WeeklyDays = [DayOfWeek.Monday],
            End = RecurrenceEnd.AfterCount,
            Count = 10,
        }).Should().Be("FREQ=WEEKLY;BYDAY=MO;COUNT=10");
    }

    [Fact]
    public void A_weekly_rule_can_be_read_back()
    {
        var options = new RecurrenceRuleOptions
        {
            Frequency = RecurrenceFrequency.Weekly,
            Interval = 2,
            WeeklyDays = [DayOfWeek.Monday, DayOfWeek.Friday],
            End = RecurrenceEnd.AfterCount,
            Count = 10,
        };

        var rule = RecurrenceRuleBuilder.Build(options);
        var parsed = RecurrenceRuleBuilder.TryParse(rule, out var result);

        parsed.Should().BeTrue();
        result.Frequency.Should().Be(RecurrenceFrequency.Weekly);
        result.Interval.Should().Be(2);
        result.WeeklyDays.Should().Equal(DayOfWeek.Monday, DayOfWeek.Friday);
        result.End.Should().Be(RecurrenceEnd.AfterCount);
        result.Count.Should().Be(10);
    }

    [Fact]
    public void A_nth_weekday_rule_can_be_read_back()
    {
        RecurrenceRuleBuilder.TryParse("FREQ=MONTHLY;BYDAY=2TU", out var result).Should().BeTrue();

        result.Frequency.Should().Be(RecurrenceFrequency.Monthly);
        result.MonthlyPattern.Should().Be(MonthlyPattern.ByWeekday);
        result.MonthlyWeekdayOrdinal.Should().Be(2);
        result.MonthlyWeekday.Should().Be(DayOfWeek.Tuesday);
    }

    [Fact]
    public void An_until_rule_can_be_read_back()
    {
        RecurrenceRuleBuilder.TryParse("FREQ=DAILY;UNTIL=20261231T235959", out var result).Should().BeTrue();

        result.End.Should().Be(RecurrenceEnd.OnDate);
        result.EndDate.Should().Be(new DateOnly(2026, 12, 31));
    }

    [Fact]
    public void A_yearly_rule_can_be_read_back()
    {
        RecurrenceRuleBuilder.TryParse("FREQ=YEARLY;BYMONTH=3;BYMONTHDAY=15", out var result).Should().BeTrue();

        result.Frequency.Should().Be(RecurrenceFrequency.Yearly);
        result.YearlyMonth.Should().Be(3);
        result.MonthlyDay.Should().Be(15);
    }

    [Fact]
    public void A_prefixed_ical_rule_can_be_read_back()
    {
        RecurrenceRuleBuilder.TryParse("RRULE:FREQ=DAILY;INTERVAL=2", out var result).Should().BeTrue();

        result.Frequency.Should().Be(RecurrenceFrequency.Daily);
        result.Interval.Should().Be(2);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("NOTA=RULE")]
    public void An_unusable_rule_cannot_be_parsed(string? rule)
    {
        RecurrenceRuleBuilder.TryParse(rule, out _).Should().BeFalse();
    }

    [Fact]
    public void A_rule_describes_itself_in_words()
    {
        RecurrenceRuleBuilder.Describe(new RecurrenceRuleOptions
        {
            Frequency = RecurrenceFrequency.Weekly,
            WeeklyDays = [DayOfWeek.Monday, DayOfWeek.Wednesday],
        }).Should().Be("Every week on Monday, Wednesday");
    }
}
