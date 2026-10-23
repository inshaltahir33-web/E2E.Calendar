using E2E.Calendar.Core.Models;
using E2E.Calendar.Core.Recurrence;
using E2E.Calendar.Core.Services;
using FluentAssertions;
using Xunit;

namespace E2E.Calendar.Core.Tests;

public sealed class DemoScheduleTests
{
    private static readonly DateTimeOffset FirstMondayUtc = new(2026, 10, 12, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void The_demo_defines_each_recurring_series_once()
    {
        var recurring = DemoSchedule.Recurring(new DateOnly(2026, 10, 12));

        recurring.Should().HaveCount(2);
        recurring.Select(series => series.Title).Should().OnlyHaveUniqueItems();
        recurring.Should().OnlyContain(series => !string.IsNullOrWhiteSpace(series.RRule));
    }

    [Fact]
    public void A_single_weekly_master_produces_one_occurrence_per_weekday()
    {
        var series = DemoSchedule.Recurring(new DateOnly(2026, 10, 12))
            .Single(item => item.Title == "Daily standup");

        var master = new CalendarEvent
        {
            Id = "demo-standup",
            CalendarId = "demo-cal-work",
            Title = series.Title,
            StartUtc = new DateTimeOffset(
                series.Date.ToDateTime(new TimeOnly(series.Hour, series.Minute)),
                TimeSpan.Zero),
            EndUtc = new DateTimeOffset(
                series.Date.ToDateTime(new TimeOnly(series.Hour, series.Minute)),
                TimeSpan.Zero).AddMinutes(series.DurationMinutes),
            TimeZoneId = "UTC",
            RRule = series.RRule,
            CreatedUtc = FirstMondayUtc,
            UpdatedUtc = FirstMondayUtc,
        };

        var from = FirstMondayUtc;
        var to = FirstMondayUtc.AddDays(21);

        var occurrences = new RecurrenceExpander()
            .Expand(master, Array.Empty<CalendarEvent>(), from, to)
            .ToList();

        // Three full weeks of a Monday-Friday series: one occurrence per weekday,
        // never the master plus an extra occurrence at the same instant.
        occurrences.Should().HaveCount(15);
        occurrences.Select(occurrence => occurrence.StartUtc).Should().OnlyHaveUniqueItems();
    }
}
