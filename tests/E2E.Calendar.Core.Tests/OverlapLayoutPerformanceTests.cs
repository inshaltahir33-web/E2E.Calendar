using System.Diagnostics;
using E2E.Calendar.Core.Models;
using E2E.Calendar.Core.Services;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace E2E.Calendar.Core.Tests;

public sealed class OverlapLayoutPerformanceTests
{
    private const int EventCount = 10_000;

    private static readonly DateTimeOffset DayStart = new(2026, 10, 19, 0, 0, 0, TimeSpan.Zero);

    private readonly ITestOutputHelper _output;

    public OverlapLayoutPerformanceTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private static List<CalendarItem> CreateItems()
    {
        var items = new List<CalendarItem>(EventCount);
        var random = new Random(1234);

        for (var index = 0; index < EventCount; index++)
        {
            var day = DayStart.AddDays(index % 90);
            var startMinute = random.Next(0, 22 * 60);
            var duration = random.Next(15, 180);

            items.Add(new CalendarItem
            {
                Id = $"perf-{index}",
                Title = $"Event {index}",
                StartUtc = day.AddMinutes(startMinute),
                EndUtc = day.AddMinutes(startMinute + duration),
            });
        }

        return items;
    }

    [Fact]
    public void Packing_ten_thousand_events_stays_under_two_hundred_milliseconds()
    {
        var items = CreateItems();

        var stopwatch = Stopwatch.StartNew();
        var packed = OverlapLayout.Pack(items, DayStart, DayStart.AddDays(90));
        stopwatch.Stop();

        packed.Should().HaveCount(EventCount);
        packed.Should().OnlyContain(entry => entry.Column >= 0);
        packed.Select(entry => entry.ClusterIndex).Distinct().Should().HaveCountGreaterThan(0);

        _output.WriteLine(
            $"Packed {EventCount} events into {packed.Select(entry => entry.ClusterIndex).Distinct().Count()} clusters in {stopwatch.ElapsedMilliseconds} ms.");

        stopwatch.ElapsedMilliseconds.Should().BeLessThan(200);
    }

    [Fact]
    public void Packing_a_single_visible_day_only_keeps_the_items_of_that_day()
    {
        var items = CreateItems();
        var day = DayStart.AddDays(3);
        var dayEnd = day.AddDays(1);

        // The grid filters the visible range first, then packs what is left.
        var visible = items.Where(item => item.StartUtc < dayEnd && item.EndUtc > day).ToList();
        var packed = OverlapLayout.Pack(visible, day, dayEnd);

        packed.Should().NotBeEmpty();
        packed.Should().OnlyContain(entry => entry.Item.StartUtc < dayEnd && entry.Item.EndUtc > day);
        packed.Should().OnlyContain(entry => entry.StartFraction >= 0 && entry.StartFraction <= 1);
    }
}
