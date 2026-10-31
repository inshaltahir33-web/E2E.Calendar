using E2E.Calendar.Core.Abstractions;
using E2E.Calendar.Core.Models;
using E2E.Calendar.Core.QuickAdd;
using FluentAssertions;
using Xunit;

namespace E2E.Calendar.Core.Tests;

public sealed class QuickAddParserTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 23, 9, 0, 0, TimeSpan.Zero);

    private static readonly IClock Clock = new FixedClock(Now, TimeZoneInfo.Utc);

    [Fact]
    public void A_full_event_phrase_extracts_title_date_time_and_place()
    {
        var item = QuickAddParser.Parse("Lunch with Ali tomorrow 1pm at Kohinoor", Clock);

        item.Kind.Should().Be(ParsedItemKind.Event);
        item.Title.Should().Be("Lunch with Ali");
        item.Date.Should().Be(new DateOnly(2026, 10, 24));
        item.StartTime.Should().Be(new TimeOnly(13, 0));
        item.EndTime.Should().Be(new TimeOnly(14, 0));
        item.Location.Should().Be("Kohinoor");
        item.AllDay.Should().BeFalse();
        item.StartUtc.Should().Be(new DateTimeOffset(2026, 10, 24, 13, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public void A_weekly_repeat_with_a_duration_is_parsed()
    {
        var item = QuickAddParser.Parse("Standup every monday at 9:30 for 30 min", Clock);

        item.Title.Should().Be("Standup");
        item.RRule.Should().Be("FREQ=WEEKLY;BYDAY=MO");
        item.StartTime.Should().Be(new TimeOnly(9, 30));
        item.EndTime.Should().Be(new TimeOnly(10, 0));
    }

    [Fact]
    public void A_remind_me_phrase_becomes_a_reminder_with_a_lead_time()
    {
        var item = QuickAddParser.Parse("remind me 30 min before dentist tomorrow 4pm", Clock);

        item.Kind.Should().Be(ParsedItemKind.Reminder);
        item.Title.Should().Be("dentist");
        item.AlertOffsetsMinutes.Should().ContainSingle().Which.Should().Be(30);
        item.StartTime.Should().Be(new TimeOnly(16, 0));
    }

    [Fact]
    public void Relative_minutes_are_anchored_to_the_clock()
    {
        var item = QuickAddParser.Parse("Call bank in 10 minutes", Clock);

        item.Title.Should().Be("Call bank");
        item.StartUtc.Should().Be(Now.AddMinutes(10));
        item.StartTime.Should().Be(new TimeOnly(9, 10));
    }

    [Fact]
    public void A_bare_in_phrase_becomes_a_one_shot_countdown_reminder()
    {
        var item = QuickAddParser.Parse("in 25 minutes", Clock);

        item.Kind.Should().Be(ParsedItemKind.Reminder);
        item.Title.Should().Be("Reminder in 25 min");
        item.StartUtc.Should().Be(Now.AddMinutes(25));
        item.RRule.Should().BeNull();
        item.AlertOffsetsMinutes.Should().ContainSingle().Which.Should().Be(0);
    }

    [Fact]
    public void Tonight_anchors_to_the_evening_of_today()
    {
        var item = QuickAddParser.Parse("Dinner tonight at 8pm", Clock);

        item.Date.Should().Be(new DateOnly(2026, 10, 23));
        item.StartTime.Should().Be(new TimeOnly(20, 0));
        item.Title.Should().Be("Dinner");
    }

    [Fact]
    public void Next_weekday_selects_the_following_week()
    {
        var item = QuickAddParser.Parse("Project deadline next friday", Clock);

        item.Date.Should().Be(new DateOnly(2026, 10, 30));
        item.AllDay.Should().BeTrue();
        item.Title.Should().Be("Project deadline");
    }

    [Fact]
    public void A_slash_date_is_parsed()
    {
        var item = QuickAddParser.Parse("Holiday 25/12", Clock);

        item.Date.Should().Be(new DateOnly(2026, 12, 25));
        item.AllDay.Should().BeTrue();
        item.Title.Should().Be("Holiday");
    }

    [Fact]
    public void An_iso_date_and_duration_are_parsed()
    {
        var item = QuickAddParser.Parse("Conference 2026-11-05 10:00 for 2 hours", Clock);

        item.Date.Should().Be(new DateOnly(2026, 11, 5));
        item.StartTime.Should().Be(new TimeOnly(10, 0));
        item.EndTime.Should().Be(new TimeOnly(12, 0));
        item.Title.Should().Be("Conference");
    }

    [Fact]
    public void Multiple_weekdays_are_parsed()
    {
        var item = QuickAddParser.Parse("Yoga every week on mon, wed at 7am", Clock);

        item.RRule.Should().Be("FREQ=WEEKLY;BYDAY=MO,WE");
        item.StartTime.Should().Be(new TimeOnly(7, 0));
    }

    [Fact]
    public void A_hash_tag_sets_the_calendar_hint()
    {
        var item = QuickAddParser.Parse("Buy milk #personal", Clock);

        item.Title.Should().Be("Buy milk");
        item.CalendarHint.Should().Be("personal");
    }

    [Fact]
    public void A_bang_token_sets_the_priority()
    {
        var item = QuickAddParser.Parse("Fix login bug !high", Clock);

        item.Title.Should().Be("Fix login bug");
        item.Priority.Should().Be(Priority.High);
    }

    [Fact]
    public void Remind_me_to_produces_a_nine_am_reminder()
    {
        var item = QuickAddParser.Parse("Remind me to call mom", Clock);

        item.Kind.Should().Be(ParsedItemKind.Reminder);
        item.Title.Should().Be("call mom");
        item.StartTime.Should().Be(new TimeOnly(9, 0));
        item.AlertOffsetsMinutes.Should().ContainSingle().Which.Should().Be(0);
    }

    [Fact]
    public void A_future_clock_time_stays_on_today()
    {
        var item = QuickAddParser.Parse("Team lunch 1pm at Kohinoor", Clock);

        item.Date.Should().Be(new DateOnly(2026, 10, 23));
        item.Location.Should().Be("Kohinoor");
    }

    [Fact]
    public void A_clock_time_that_already_passed_rolls_to_tomorrow()
    {
        var item = QuickAddParser.Parse("Breakfast at 8am", Clock);

        item.Date.Should().Be(new DateOnly(2026, 10, 24));
        item.StartTime.Should().Be(new TimeOnly(8, 0));
    }

    [Fact]
    public void A_date_without_a_time_is_all_day()
    {
        var item = QuickAddParser.Parse("Call dentist tomorrow", Clock);

        item.Date.Should().Be(new DateOnly(2026, 10, 24));
        item.AllDay.Should().BeTrue();
        item.Title.Should().Be("Call dentist");
    }

    [Fact]
    public void Every_day_maps_to_a_daily_rule()
    {
        QuickAddParser.Parse("Stretch every day at 8am", Clock)
            .RRule.Should().Be("FREQ=DAILY");
    }

    [Fact]
    public void Every_n_weeks_maps_to_an_interval()
    {
        QuickAddParser.Parse("Review every 2 weeks", Clock)
            .RRule.Should().Be("FREQ=WEEKLY;INTERVAL=2");
    }

    [Fact]
    public void Every_weekday_maps_to_a_workday_rule()
    {
        QuickAddParser.Parse("Inbox zero every weekday at 9:00", Clock)
            .RRule.Should().Be("FREQ=WEEKLY;BYDAY=MO,TU,WE,TH,FR");
    }

    [Fact]
    public void A_full_numeric_date_is_parsed()
    {
        QuickAddParser.Parse("Party 31/12/2026", Clock)
            .Date.Should().Be(new DateOnly(2026, 12, 31));
    }

    [Fact]
    public void Remind_me_in_minutes_is_a_reminder()
    {
        var item = QuickAddParser.Parse("Remind me in 30 min", Clock);

        item.Kind.Should().Be(ParsedItemKind.Reminder);
        item.StartUtc.Should().Be(Now.AddMinutes(30));
    }

    [Fact]
    public void Lunch_for_ninety_minutes_uses_the_duration()
    {
        var item = QuickAddParser.Parse("Lunch for 90 min tomorrow at noon", Clock);

        item.StartTime.Should().Be(new TimeOnly(12, 0));
        item.EndTime.Should().Be(new TimeOnly(13, 30));
    }

    [Fact]
    public void A_plain_weekday_resolves_to_the_next_occurrence()
    {
        var item = QuickAddParser.Parse("Meeting monday 3pm", Clock);

        item.Date.Should().Be(new DateOnly(2026, 10, 26));
        item.StartTime.Should().Be(new TimeOnly(15, 0));
    }

    [Fact]
    public void An_empty_phrase_still_produces_a_usable_item()
    {
        var item = QuickAddParser.Parse("   ", Clock);

        item.Title.Should().Be("Untitled");
        item.AllDay.Should().BeTrue();
        item.Date.Should().Be(new DateOnly(2026, 10, 23));
    }

    [Fact]
    public void A_location_without_a_time_is_still_extracted()
    {
        var item = QuickAddParser.Parse("Dinner at Taj tomorrow", Clock);

        item.Location.Should().Be("Taj");
        item.Title.Should().Be("Dinner");
        item.Date.Should().Be(new DateOnly(2026, 10, 24));
    }

    [Fact]
    public void An_explicit_year_is_kept()
    {
        QuickAddParser.Parse("Trip 5 Jan 2027", Clock)
            .Date.Should().Be(new DateOnly(2027, 1, 5));
    }

    [Fact]
    public void A_pm_time_before_noon_is_shifted_to_the_afternoon()
    {
        QuickAddParser.Parse("Call 12pm", Clock)
            .StartTime.Should().Be(new TimeOnly(12, 0));
    }

    [Fact]
    public void A_midnight_time_is_supported()
    {
        QuickAddParser.Parse("Deploy at midnight", Clock)
            .StartTime.Should().Be(new TimeOnly(0, 0));
    }

    private sealed class FixedClock : IClock
    {
        public FixedClock(DateTimeOffset utcNow, TimeZoneInfo timeZone)
        {
            UtcNow = utcNow;
            LocalTimeZone = timeZone;
        }

        public DateTimeOffset UtcNow { get; }

        public TimeZoneInfo LocalTimeZone { get; }
    }
}
