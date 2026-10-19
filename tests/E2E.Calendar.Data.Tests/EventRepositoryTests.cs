using Dapper;
using E2E.Calendar.Core.Models;
using E2E.Calendar.Data.Repos;
using FluentAssertions;
using Xunit;

namespace E2E.Calendar.Data.Tests;

public sealed class EventRepositoryTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Start = new(2026, 10, 21, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset End = new(2026, 10, 21, 9, 30, 0, TimeSpan.Zero);

    private readonly TempDatabase _database = new();
    private EventRepository _events = null!;

    public async Task InitializeAsync()
    {
        await _database.InitializeAsync();
        _events = new EventRepository(_database.Database);
    }

    public async Task DisposeAsync()
    {
        await _database.DisposeAsync();
    }

    private static CalendarEvent CreateEvent(
        string id = "evt-1",
        string calendarId = "cal-local",
        DateTimeOffset? startUtc = null,
        DateTimeOffset? endUtc = null,
        string? rrule = null,
        bool deleted = false) =>
        new()
        {
            Id = id,
            CalendarId = calendarId,
            RemoteId = "remote-1",
            ETag = "etag-1",
            Title = "Standup",
            Notes = "Room 3",
            Location = "Kohinoor",
            Url = "https://example.invalid/standup",
            StartUtc = startUtc ?? Start,
            EndUtc = endUtc ?? End,
            AllDay = false,
            TimeZoneId = "Asia/Kolkata",
            RRule = rrule,
            ExDates = null,
            RecurrenceMasterId = null,
            RecurrenceIdUtc = null,
            Color = "#FF0000",
            MeetingUrl = "https://example.invalid/meet",
            TravelMinutes = 15,
            CreatedUtc = new DateTimeOffset(2026, 10, 19, 8, 0, 0, TimeSpan.Zero),
            UpdatedUtc = new DateTimeOffset(2026, 10, 19, 8, 0, 0, TimeSpan.Zero),
            Deleted = deleted,
            Dirty = true,
        };

    [Fact]
    public async Task An_event_round_trips_through_the_repository()
    {
        var original = CreateEvent();

        await _events.UpsertAsync(original);
        var loaded = await _events.GetByIdAsync(original.Id);

        loaded.Should().NotBeNull();
        loaded!.Id.Should().Be(original.Id);
        loaded.CalendarId.Should().Be(original.CalendarId);
        loaded.RemoteId.Should().Be(original.RemoteId);
        loaded.ETag.Should().Be(original.ETag);
        loaded.Title.Should().Be(original.Title);
        loaded.Notes.Should().Be(original.Notes);
        loaded.Location.Should().Be(original.Location);
        loaded.Url.Should().Be(original.Url);
        loaded.StartUtc.Should().Be(original.StartUtc);
        loaded.EndUtc.Should().Be(original.EndUtc);
        loaded.AllDay.Should().Be(original.AllDay);
        loaded.TimeZoneId.Should().Be(original.TimeZoneId);
        loaded.ExDates.Should().BeNull();
        loaded.RecurrenceMasterId.Should().BeNull();
        loaded.RecurrenceIdUtc.Should().BeNull();
        loaded.Color.Should().Be(original.Color);
        loaded.MeetingUrl.Should().Be(original.MeetingUrl);
        loaded.TravelMinutes.Should().Be(original.TravelMinutes);
        loaded.CreatedUtc.Should().Be(original.CreatedUtc);
        loaded.UpdatedUtc.Should().Be(original.UpdatedUtc);
        loaded.Deleted.Should().BeFalse();
        loaded.Dirty.Should().BeTrue();
    }

    [Fact]
    public async Task An_event_that_does_not_exist_is_returned_as_null()
    {
        var loaded = await _events.GetByIdAsync("missing");

        loaded.Should().BeNull();
    }

    [Fact]
    public async Task Upserting_an_existing_event_updates_it_in_place()
    {
        await _events.UpsertAsync(CreateEvent());

        var updated = CreateEvent() with { Title = "Daily standup", Dirty = false };
        await _events.UpsertAsync(updated);

        var loaded = await _events.GetByIdAsync("evt-1");

        loaded!.Title.Should().Be("Daily standup");
        loaded.Dirty.Should().BeFalse();
    }

    [Fact]
    public async Task Timestamps_are_stored_as_utc_iso_8601_text()
    {
        await _events.UpsertAsync(CreateEvent());

        await using var connection = await _database.Database.OpenAsync();
        var stored = (await connection.QueryAsync<string>(
            "SELECT start_utc FROM event WHERE id = 'evt-1';")).Single();

        stored.Should().Be("2026-10-21T09:00:00.0000000Z");
    }

    [Fact]
    public async Task A_range_query_returns_only_the_overlapping_events()
    {
        await _events.UpsertAsync(CreateEvent(id: "overlapping"));
        await _events.UpsertAsync(CreateEvent(
            id: "before",
            startUtc: new DateTimeOffset(2026, 10, 19, 9, 0, 0, TimeSpan.Zero),
            endUtc: new DateTimeOffset(2026, 10, 19, 9, 30, 0, TimeSpan.Zero)));
        await _events.UpsertAsync(CreateEvent(
            id: "after",
            startUtc: new DateTimeOffset(2026, 10, 23, 9, 0, 0, TimeSpan.Zero),
            endUtc: new DateTimeOffset(2026, 10, 23, 9, 30, 0, TimeSpan.Zero)));

        var results = await _events.GetInRangeAsync(
            "cal-local",
            new DateTimeOffset(2026, 10, 21, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 10, 22, 0, 0, 0, TimeSpan.Zero));

        results.Select(e => e.Id).Should().Equal("overlapping");
    }

    [Fact]
    public async Task A_range_query_skips_soft_deleted_events()
    {
        await _events.UpsertAsync(CreateEvent(id: "deleted", deleted: true));

        var results = await _events.GetInRangeAsync(
            "cal-local",
            new DateTimeOffset(2026, 10, 20, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 10, 22, 0, 0, 0, TimeSpan.Zero));

        results.Should().BeEmpty();
    }

    [Fact]
    public async Task A_range_query_returns_a_recurring_master_whose_own_window_has_already_ended()
    {
        await _events.UpsertAsync(CreateEvent(
            id: "master",
            rrule: "FREQ=WEEKLY;BYDAY=MO",
            startUtc: new DateTimeOffset(2026, 1, 5, 9, 0, 0, TimeSpan.Zero),
            endUtc: new DateTimeOffset(2026, 1, 5, 9, 30, 0, TimeSpan.Zero)));

        var results = await _events.GetInRangeAsync(
            "cal-local",
            new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 3, 8, 0, 0, 0, TimeSpan.Zero));

        results.Select(e => e.Id).Should().Equal("master");
    }

    [Fact]
    public async Task A_range_query_can_span_every_calendar()
    {
        await _events.UpsertAsync(CreateEvent(id: "one", calendarId: "cal-a"));
        await _events.UpsertAsync(CreateEvent(id: "two", calendarId: "cal-b"));

        var results = await _events.GetInRangeAsync(
            null,
            new DateTimeOffset(2026, 10, 20, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 10, 22, 0, 0, 0, TimeSpan.Zero));

        results.Select(e => e.Id).Should().BeEquivalentTo(["one", "two"]);
    }

    [Fact]
    public async Task Soft_deleted_events_can_be_listed_for_purging()
    {
        await _events.UpsertAsync(CreateEvent(id: "gone", deleted: true));

        var results = await _events.GetSoftDeletedAsync(
            new DateTimeOffset(2026, 10, 25, 0, 0, 0, TimeSpan.Zero));

        results.Select(e => e.Id).Should().Equal("gone");
    }

    [Fact]
    public async Task Events_can_be_inserted_in_bulk()
    {
        var events = Enumerable.Range(0, 250)
            .Select(i => CreateEvent(
                id: $"bulk-{i}",
                startUtc: Start.AddDays(i),
                endUtc: End.AddDays(i)))
            .ToList();

        await _events.InsertManyAsync(events);

        var results = await _events.GetInRangeAsync(
            "cal-local",
            new DateTimeOffset(2026, 10, 21, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2027, 7, 1, 0, 0, 0, TimeSpan.Zero));

        results.Should().HaveCount(250);
    }
}
