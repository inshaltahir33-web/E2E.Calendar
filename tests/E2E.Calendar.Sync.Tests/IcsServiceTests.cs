using E2E.Calendar.Core.Models;
using E2E.Calendar.Sync.Ics;
using FluentAssertions;
using Xunit;

namespace E2E.Calendar.Sync.Tests;

public sealed class IcsServiceTests
{
    [Fact]
    public void Export_and_import_round_trip_event_and_rrule()
    {
        var service = new IcsService();
        var source = new CalendarEvent
        {
            Id = "event-1",
            CalendarId = "cal-local",
            Title = "Planning",
            StartUtc = new DateTimeOffset(2026, 10, 25, 9, 0, 0, TimeSpan.Zero),
            EndUtc = new DateTimeOffset(2026, 10, 25, 10, 0, 0, TimeSpan.Zero),
            TimeZoneId = "UTC",
            RRule = "FREQ=WEEKLY;BYDAY=SU",
            CreatedUtc = DateTimeOffset.UtcNow,
            UpdatedUtc = DateTimeOffset.UtcNow,
        };

        var imported = service.Import(service.Export([source]), "cal-imported", DateTimeOffset.UtcNow);

        imported.Should().ContainSingle();
        imported[0].Title.Should().Be(source.Title);
        imported[0].CalendarId.Should().Be("cal-imported");
        imported[0].RRule.Should().Contain("FREQ=WEEKLY");
    }

    [Fact]
    public void Export_includes_every_event_and_parses_back()
    {
        var service = new IcsService();
        var events = Enumerable.Range(1, 7).Select(index => new CalendarEvent
        {
            Id = $"event-{index}",
            CalendarId = "cal-local",
            Title = $"Event {index}",
            StartUtc = new DateTimeOffset(2026, 10, 25, 9, 0, 0, TimeSpan.Zero).AddDays(index),
            EndUtc = new DateTimeOffset(2026, 10, 25, 10, 0, 0, TimeSpan.Zero).AddDays(index),
            TimeZoneId = "UTC",
            AllDay = index % 2 == 0,
            CreatedUtc = DateTimeOffset.UtcNow,
            UpdatedUtc = DateTimeOffset.UtcNow,
        }).ToList();

        var ics = service.Export(events);

        (ics.Split(new[] { "BEGIN:VEVENT" }, StringSplitOptions.None).Length - 1).Should().Be(events.Count);
        ics.Should().Contain("\r\n");

        var imported = service.Import(ics, "cal-imported", DateTimeOffset.UtcNow);
        imported.Should().HaveCount(events.Count);
    }
}
