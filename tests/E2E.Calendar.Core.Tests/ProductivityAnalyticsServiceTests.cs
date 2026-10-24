using E2E.Calendar.Core.Abstractions;
using E2E.Calendar.Core.Models;
using E2E.Calendar.Core.Services;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace E2E.Calendar.Core.Tests;

public sealed class ProductivityAnalyticsServiceTests
{
    [Fact]
    public async Task Counts_ended_events_and_groups_duration_by_category()
    {
        var repository = Substitute.For<IEventRepository>();
        var from = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var now = from.AddDays(2);
        repository.GetInRangeAsync(null, from, now, Arg.Any<CancellationToken>())
            .Returns(
            [
                Event("one", from.AddHours(1), from.AddHours(2), "Focus"),
                Event("two", from.AddHours(3), from.AddHours(5), "Focus"),
                Event("future", now.AddHours(1), now.AddHours(2), "Focus"),
                Event("other", from.AddHours(6), from.AddHours(7), null),
            ]);

        var result = await new ProductivityAnalyticsService(repository)
            .GetMetricsAsync(from, now, now);

        result.CompletedEventCount.Should().Be(3);
        result.CompletedDuration.Should().Be(TimeSpan.FromHours(4));
        result.DurationByCategory["Focus"].Should().Be(TimeSpan.FromHours(3));
        result.DurationByCategory["Uncategorised"].Should().Be(TimeSpan.FromHours(1));
    }

    private static CalendarEvent Event(string id, DateTimeOffset start, DateTimeOffset end, string? category) => new()
    {
        Id = id,
        CalendarId = "calendar",
        Title = id,
        StartUtc = start,
        EndUtc = end,
        TimeZoneId = "UTC",
        Category = category,
        CreatedUtc = start,
        UpdatedUtc = start,
    };
}
