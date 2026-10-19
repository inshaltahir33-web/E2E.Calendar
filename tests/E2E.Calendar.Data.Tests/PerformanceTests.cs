using System.Diagnostics;
using E2E.Calendar.Core.Models;
using E2E.Calendar.Data.Repos;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace E2E.Calendar.Data.Tests;

public sealed class PerformanceTests : IAsyncLifetime
{
    private const int EventCount = 10_000;
    private const string CalendarId = "cal-benchmark";

    private static readonly DateTimeOffset YearStart = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset MonthStart = new(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset MonthEnd = new(2026, 7, 1, 0, 0, 0, TimeSpan.Zero);

    private readonly ITestOutputHelper _output;
    private readonly TempDatabase _database = new();
    private EventRepository _events = null!;

    public PerformanceTests(ITestOutputHelper output)
    {
        _output = output;
    }

    public async Task InitializeAsync()
    {
        await _database.InitializeAsync();
        _events = new EventRepository(_database.Database);
    }

    public async Task DisposeAsync()
    {
        await _database.DisposeAsync();
    }

    private static CalendarEvent CreateEvent(int index)
    {
        var start = YearStart.AddMinutes(index * 52);

        return new CalendarEvent
        {
            Id = $"perf-{index}",
            CalendarId = CalendarId,
            RemoteId = null,
            ETag = null,
            Title = $"Event {index}",
            Notes = "Performance fixture",
            Location = null,
            Url = null,
            StartUtc = start,
            EndUtc = start.AddMinutes(30),
            AllDay = false,
            TimeZoneId = "Asia/Kolkata",
            RRule = null,
            ExDates = null,
            RecurrenceMasterId = null,
            RecurrenceIdUtc = null,
            Color = null,
            MeetingUrl = null,
            TravelMinutes = null,
            CreatedUtc = YearStart,
            UpdatedUtc = YearStart,
            Deleted = false,
            Dirty = true,
        };
    }

    [Fact]
    public async Task A_month_range_query_over_ten_thousand_events_stays_under_fifty_milliseconds()
    {
        var events = Enumerable.Range(0, EventCount).Select(CreateEvent).ToList();

        await _events.InsertManyAsync(events);

        await _events.GetInRangeAsync(CalendarId, MonthStart, MonthEnd);

        var stopwatch = Stopwatch.StartNew();
        var results = await _events.GetInRangeAsync(CalendarId, MonthStart, MonthEnd);
        stopwatch.Stop();

        results.Should().NotBeEmpty();
        _output.WriteLine($"Month range query over {EventCount} events returned {results.Count} rows in {stopwatch.ElapsedMilliseconds} ms.");
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(50);
    }
}
