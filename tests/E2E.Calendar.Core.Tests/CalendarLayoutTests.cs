using E2E.Calendar.Core.Models;
using E2E.Calendar.Core.Services;
using FluentAssertions;
using Xunit;

namespace E2E.Calendar.Core.Tests;

public sealed class CalendarLayoutTests
{
    private static readonly DateOnly Monday = new(2026, 10, 26);
    private static readonly DateOnly Wednesday = new(2026, 10, 28);
    private static readonly DateOnly WeekEnd = new(2026, 11, 1);

    [Fact]
    public void Two_overlapping_events_share_the_day_in_two_columns()
    {
        var result = CalendarLayout.Build(
            [
                Event("a", Wednesday, 9, 11),
                Event("b", Wednesday, 10, 12),
            ],
            Wednesday,
            Wednesday);

        result.Weeks.Should().ContainSingle();
        result.Weeks[0].Banners.Should().BeEmpty();
        result.Weeks[0].HiddenBanners.Should().BeEmpty();

        var day = result.Days.Should().ContainSingle().Subject;
        day.Date.Should().Be(Wednesday);
        day.OverflowCount.Should().Be(0);
        day.Items.Should().HaveCount(2);

        var byId = day.Items.ToDictionary(item => item.Occurrence.Id);

        byId["a"].Column.Should().Be(0);
        byId["b"].Column.Should().Be(1);
        day.Items.Should().OnlyContain(item => item.ColumnCount == 2);
        byId["a"].VisualStart.Should().Be(At(Wednesday, 9));
        byId["a"].VisualEnd.Should().Be(At(Wednesday, 11));
    }

    [Fact]
    public void Three_reminders_at_the_same_time_use_three_columns_and_thirty_minutes()
    {
        var result = CalendarLayout.Build(
            [
                Reminder("a", Wednesday, 17, 21),
                Reminder("b", Wednesday, 17, 21),
                Reminder("c", Wednesday, 17, 21),
            ],
            Wednesday,
            Wednesday);

        var day = result.Days.Should().ContainSingle().Subject;

        day.Items.Should().HaveCount(3);
        day.Items.Select(item => item.Column).Should().BeEquivalentTo([0, 1, 2]);
        day.Items.Should().OnlyContain(item => item.ColumnCount == 3);
        day.Items.Should().OnlyContain(item => item.VisualStart == At(Wednesday, 17).AddMinutes(21));
        day.Items.Should().OnlyContain(item => item.VisualEnd == At(Wednesday, 17).AddMinutes(51));
        day.OverflowCount.Should().Be(0);
    }

    [Fact]
    public void A_three_day_event_crossing_a_week_boundary_is_a_banner_in_both_weeks()
    {
        var result = CalendarLayout.Build(
            [Occ("trip", new DateTime(2026, 10, 31, 10, 0, 0), new DateTime(2026, 11, 2, 12, 0, 0))],
            Monday,
            new DateOnly(2026, 11, 8));

        result.Weeks.Should().HaveCount(2);

        var first = result.Weeks[0];
        first.Start.Should().Be(Monday);
        first.End.Should().Be(WeekEnd);
        first.Banners.Should().ContainSingle();
        first.Banners[0].Occurrence.Id.Should().Be("trip");
        first.Banners[0].StartDay.Should().Be(new DateOnly(2026, 10, 31));
        first.Banners[0].EndDay.Should().Be(WeekEnd);
        first.Banners[0].Lane.Should().Be(0);
        first.Banners[0].ContinuesFromPrev.Should().BeFalse();
        first.Banners[0].ContinuesToNext.Should().BeTrue();

        var second = result.Weeks[1];
        second.Start.Should().Be(new DateOnly(2026, 11, 2));
        second.Banners.Should().ContainSingle();
        second.Banners[0].StartDay.Should().Be(new DateOnly(2026, 11, 2));
        second.Banners[0].EndDay.Should().Be(new DateOnly(2026, 11, 2));
        second.Banners[0].Lane.Should().Be(0);
        second.Banners[0].ContinuesFromPrev.Should().BeTrue();
        second.Banners[0].ContinuesToNext.Should().BeFalse();

        result.Days.Should().HaveCount(14);
        result.Days.Should().OnlyContain(day => day.Items.Count == 0);
    }

    [Fact]
    public void An_overnight_event_is_split_across_the_two_days_it_touches()
    {
        var result = CalendarLayout.Build(
            [Event("late", new DateOnly(2026, 10, 30), 22, 26)],
            new DateOnly(2026, 10, 29),
            WeekEnd);

        result.Weeks.Should().OnlyContain(week => week.Banners.Count == 0);

        var days = result.Days.Where(day => day.Items.Count > 0).ToList();
        days.Should().HaveCount(2);

        days[0].Date.Should().Be(new DateOnly(2026, 10, 30));
        days[0].Items.Should().ContainSingle();
        days[0].Items[0].Occurrence.Id.Should().Be("late");
        days[0].Items[0].VisualStart.Should().Be(new DateTime(2026, 10, 30, 22, 0, 0));
        days[0].Items[0].VisualEnd.Should().Be(new DateTime(2026, 10, 31, 0, 0, 0));
        days[0].Items[0].Column.Should().Be(0);
        days[0].Items[0].ColumnCount.Should().Be(1);

        days[1].Date.Should().Be(new DateOnly(2026, 10, 31));
        days[1].Items.Should().ContainSingle();
        days[1].Items[0].Occurrence.Id.Should().Be("late");
        days[1].Items[0].VisualStart.Should().Be(new DateTime(2026, 10, 31, 0, 0, 0));
        days[1].Items[0].VisualEnd.Should().Be(new DateTime(2026, 10, 31, 2, 0, 0));
        days[1].Items[0].Column.Should().Be(0);
        days[1].Items[0].ColumnCount.Should().Be(1);
    }

    [Fact]
    public void Forty_reminders_in_one_day_overflow_past_max_columns()
    {
        var reminders = Enumerable.Range(0, 40)
            .Select(index => Reminder($"r{index:00}", Wednesday, 17, 21))
            .ToList();

        var result = CalendarLayout.Build(
            reminders,
            Wednesday,
            Wednesday,
            WeekStartDay.Monday,
            maxBannerLanes: 2,
            maxColumns: 3);

        var day = result.Days.Should().ContainSingle().Subject;

        day.Items.Should().HaveCount(3);
        day.Items.Select(item => item.Column).Should().BeEquivalentTo([0, 1, 2]);
        day.Items.Should().OnlyContain(item => item.ColumnCount == 3);
        day.OverflowCount.Should().Be(37);
        day.Hidden.Should().HaveCount(37);
        day.Hidden.Select(item => item.Occurrence.Id).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void Banner_lanes_beyond_max_lanes_are_counted_per_day()
    {
        var result = CalendarLayout.Build(
            [
                AllDay("a", new DateOnly(2026, 10, 28), new DateOnly(2026, 10, 29)),
                AllDay("b", new DateOnly(2026, 10, 28), new DateOnly(2026, 10, 29)),
                AllDay("c", new DateOnly(2026, 10, 28), new DateOnly(2026, 10, 28)),
            ],
            Monday,
            WeekEnd,
            maxBannerLanes: 1);

        var week = result.Weeks.Should().ContainSingle().Subject;

        week.Banners.Should().ContainSingle();
        week.Banners[0].Occurrence.Id.Should().Be("a");
        week.Banners[0].StartDay.Should().Be(new DateOnly(2026, 10, 28));
        week.Banners[0].EndDay.Should().Be(new DateOnly(2026, 10, 29));
        week.Banners[0].Lane.Should().Be(0);

        week.HiddenBanners.Should().BeEquivalentTo(new[]
        {
            new BannerOverflow(new DateOnly(2026, 10, 28), 2),
            new BannerOverflow(new DateOnly(2026, 10, 29), 1),
        });

        week.HiddenPlacements.Select(placement => placement.Occurrence.Id)
            .Should().BeEquivalentTo(["b", "c"]);
    }

    [Fact]
    public void A_banner_is_clipped_to_the_requested_day_range()
    {
        var result = CalendarLayout.Build(
            [Occ("trip", new DateTime(2026, 10, 30, 15, 0, 0), new DateTime(2026, 11, 2, 10, 0, 0))],
            new DateOnly(2026, 10, 31),
            new DateOnly(2026, 10, 31));

        var week = result.Weeks.Should().ContainSingle().Subject;
        var banner = week.Banners.Should().ContainSingle().Subject;

        banner.StartDay.Should().Be(new DateOnly(2026, 10, 31));
        banner.EndDay.Should().Be(new DateOnly(2026, 10, 31));
        banner.ContinuesFromPrev.Should().BeTrue();
        banner.ContinuesToNext.Should().BeTrue();
        week.HiddenBanners.Should().BeEmpty();
    }

    [Fact]
    public void A_date_only_task_becomes_an_all_day_occurrence()
    {
        var item = new CalendarItem
        {
            Id = "task",
            Title = "Pay rent",
            StartUtc = new DateTimeOffset(2026, 10, 28, 9, 0, 0, TimeSpan.Zero),
            EndUtc = new DateTimeOffset(2026, 10, 29, 9, 0, 0, TimeSpan.Zero),
            Kind = CalendarItemKind.Task,
            HasTime = false,
        };

        var occurrence = CalendarLayout.ToOccurrence(item, TimeZoneInfo.Utc);

        occurrence.IsAllDay.Should().BeTrue();
        occurrence.StartLocal.Should().Be(new DateTime(2026, 10, 28, 9, 0, 0));
        occurrence.Kind.Should().Be(CalendarItemKind.Task);
    }

    [Fact]
    public void Longer_banners_are_laned_first_when_they_start_together()
    {
        var result = CalendarLayout.Build(
            [
                AllDay("short", Wednesday, Wednesday),
                AllDay("long", Wednesday, new DateOnly(2026, 10, 29)),
            ],
            Monday,
            WeekEnd,
            maxBannerLanes: 1);

        var week = result.Weeks.Should().ContainSingle().Subject;

        week.Banners.Should().ContainSingle();
        week.Banners[0].Occurrence.Id.Should().Be("long");
        week.Banners[0].Lane.Should().Be(0);
        week.HiddenBanners.Should().ContainSingle()
            .Which.Should().Be(new BannerOverflow(Wednesday, 1));
    }

    private static LayoutOccurrence Occ(
        string id,
        DateTime start,
        DateTime end,
        CalendarItemKind kind = CalendarItemKind.Event,
        bool isAllDay = false) =>
        new()
        {
            Id = id,
            StartLocal = start,
            EndLocal = end,
            Kind = kind,
            IsAllDay = isAllDay,
        };

    private static LayoutOccurrence Event(string id, DateOnly day, double startHour, double endHour) =>
        Occ(id, At(day, startHour), At(day, endHour));

    private static LayoutOccurrence Reminder(string id, DateOnly day, int hour, int minute) =>
        Occ(
            id,
            day.ToDateTime(new TimeOnly(hour, minute)),
            day.ToDateTime(new TimeOnly(hour, minute)),
            CalendarItemKind.Reminder);

    private static LayoutOccurrence AllDay(string id, DateOnly start, DateOnly endInclusive) =>
        Occ(
            id,
            start.ToDateTime(TimeOnly.MinValue),
            endInclusive.AddDays(1).ToDateTime(TimeOnly.MinValue),
            isAllDay: true);

    private static DateTime At(DateOnly day, double hour) =>
        day.ToDateTime(TimeOnly.MinValue).AddHours(hour);
}
