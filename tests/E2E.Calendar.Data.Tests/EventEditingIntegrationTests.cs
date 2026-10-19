namespace E2E.Calendar.Data.Tests;

using E2E.Calendar.Core.Abstractions;
using E2E.Calendar.Core.Models;
using E2E.Calendar.Core.Recurrence;
using E2E.Calendar.Core.Services;
using E2E.Calendar.Data.Repos;
using FluentAssertions;
using Xunit;

public sealed class EventEditingIntegrationTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Start = new(2026, 10, 21, 9, 0, 0, TimeSpan.Zero);

    private readonly TempDatabase _database = new();
    private readonly FakeSecretStore _secrets = new();

    private EventService _events = null!;
    private EventQueryService _query = null!;
    private AlertRepository _alerts = null!;
    private CalendarRepository _calendars = null!;

    public async Task InitializeAsync()
    {
        await _database.InitializeAsync();

        var outbox = new Outbox(_database.Database);

        _events = new EventService(
            new EventRepository(_database.Database),
            outbox,
            new SystemClock(),
            null,
            new EventQueryService(new EventRepository(_database.Database), new RecurrenceExpander()));

        _query = new EventQueryService(new EventRepository(_database.Database), new RecurrenceExpander());
        _alerts = new AlertRepository(_database.Database);
        _calendars = new CalendarRepository(_database.Database);
    }

    public async Task DisposeAsync()
    {
        await _database.DisposeAsync();
    }

    [Fact]
    public async Task An_event_saved_through_the_editor_is_stored_and_comes_back()
    {
        await _calendars.UpsertAsync(new Calendar
        {
            Id = "cal-local",
            Name = "Personal",
            Color = CalendarPalette.Personal,
            IsLocal = true,
        });

        var created = await _events.CreateAsync(new EventDraft
        {
            CalendarId = "cal-local",
            Title = "Design review",
            Notes = "Bring the mockups",
            Location = "Kohinoor",
            Url = "https://example.invalid/review",
            StartUtc = Start,
            EndUtc = Start.AddHours(1),
            TimeZoneId = "Asia/Kolkata",
            RRule = RecurrenceRuleBuilder.Build(new RecurrenceRuleOptions
            {
                Frequency = RecurrenceFrequency.Weekly,
                WeeklyDays = [DayOfWeek.Wednesday],
                End = RecurrenceEnd.AfterCount,
                Count = 4,
            }),
            Color = CalendarPalette.Work,
        });

        created.Id.Should().NotBeNullOrWhiteSpace();
        created.Dirty.Should().BeTrue();

        var stored = await new EventRepository(_database.Database).GetByIdAsync(created.Id);

        stored.Should().NotBeNull();
        stored!.Title.Should().Be("Design review");
        stored.Notes.Should().Be("Bring the mockups");
        stored.Location.Should().Be("Kohinoor");
        stored.RRule.Should().Be("FREQ=WEEKLY;BYDAY=WE;COUNT=4");
        stored.StartUtc.Should().Be(Start);
        stored.EndUtc.Should().Be(Start.AddHours(1));
        stored.Dirty.Should().BeTrue();
    }

    [Fact]
    public async Task The_alerts_of_a_saved_event_are_stored()
    {
        await _calendars.UpsertAsync(new Calendar { Id = "cal-local", Name = "Personal", IsLocal = true });

        var created = await _events.CreateAsync(new EventDraft
        {
            CalendarId = "cal-local",
            Title = "Call the bank",
            StartUtc = Start,
            EndUtc = Start.AddMinutes(30),
            TimeZoneId = "Asia/Kolkata",
        });

        await _alerts.UpsertAlertAsync(new Alert
        {
            Id = "alert-1",
            OwnerType = OwnerType.Event,
            OwnerId = created.Id,
            TriggerOffsetMinutes = 15,
            Priority = Priority.Normal,
        });

        var alerts = await _alerts.GetAlertsForOwnerAsync(OwnerType.Event, created.Id);

        alerts.Should().HaveCount(1);
        alerts[0].TriggerOffsetMinutes.Should().Be(15);
    }

    [Fact]
    public async Task A_saved_recurring_event_expands_into_occurrences()
    {
        await _calendars.UpsertAsync(new Calendar { Id = "cal-local", Name = "Personal", IsLocal = true });

        await _events.CreateAsync(new EventDraft
        {
            CalendarId = "cal-local",
            Title = "Design review",
            StartUtc = Start,
            EndUtc = Start.AddHours(1),
            TimeZoneId = "Asia/Kolkata",
            RRule = "FREQ=WEEKLY;BYDAY=WE;COUNT=4",
        });

        var items = await _query.GetItemsAsync(
            Start.AddDays(-1),
            Start.AddDays(40));

        items.Should().HaveCount(4);
        items.Select(item => item.Title).Should().OnlyContain(title => title == "Design review");
        items.Select(item => item.StartUtc.DayOfWeek).Should().OnlyContain(day => day == DayOfWeek.Wednesday);
    }

    [Fact]
    public async Task Deleting_an_event_soft_deletes_it_and_it_can_be_undone()
    {
        await _calendars.UpsertAsync(new Calendar { Id = "cal-local", Name = "Personal", IsLocal = true });

        var created = await _events.CreateAsync(new EventDraft
        {
            CalendarId = "cal-local",
            Title = "Cancelled thing",
            StartUtc = Start,
            EndUtc = Start.AddHours(1),
            TimeZoneId = "Asia/Kolkata",
        });

        await _events.SoftDeleteAsync(created.Id);

        var deleted = await new EventRepository(_database.Database).GetByIdAsync(created.Id);
        deleted!.Deleted.Should().BeTrue();

        await _events.UndoDeleteAsync(created.Id);

        var restored = await new EventRepository(_database.Database).GetByIdAsync(created.Id);
        restored!.Deleted.Should().BeFalse();
    }
}
