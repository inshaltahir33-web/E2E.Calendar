using E2E.Calendar.Core.Abstractions;
using E2E.Calendar.Core.Models;
using E2E.Calendar.Core.Services;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace E2E.Calendar.Core.Tests;

public sealed class SchedulingAssistantServiceTests
{
    [Fact]
    public async Task Excludes_overlapping_events_across_selected_calendars()
    {
        var repository = Substitute.For<IEventRepository>();
        var day = new DateTimeOffset(2026, 1, 5, 0, 0, 0, TimeSpan.Zero);
        repository.GetInRangeAsync("a", day, day.AddDays(1), Arg.Any<CancellationToken>())
            .Returns([Event("a", day.AddHours(10), day.AddHours(11))]);
        repository.GetInRangeAsync("b", day, day.AddDays(1), Arg.Any<CancellationToken>())
            .Returns([Event("b", day.AddHours(13), day.AddHours(14))]);

        var slots = await new SchedulingAssistantService(repository).FindSlotsAsync(
            ["a", "b"], day, day.AddDays(1), TimeSpan.FromHours(1), TimeSpan.FromHours(9),
            TimeSpan.FromHours(17), TimeSpan.FromHours(1));

        slots.Should().NotContain(slot => slot.StartUtc == day.AddHours(10));
        slots.Should().NotContain(slot => slot.StartUtc == day.AddHours(13));
        slots.Should().Contain(slot => slot.StartUtc == day.AddHours(9));
    }

    private static CalendarEvent Event(string id, DateTimeOffset start, DateTimeOffset end) => new()
    {
        Id = id,
        CalendarId = id,
        Title = id,
        StartUtc = start,
        EndUtc = end,
        TimeZoneId = "UTC",
        CreatedUtc = start,
        UpdatedUtc = start,
    };
}
