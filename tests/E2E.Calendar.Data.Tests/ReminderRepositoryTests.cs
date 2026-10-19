using E2E.Calendar.Core.Models;
using E2E.Calendar.Data.Repos;
using FluentAssertions;
using Xunit;

namespace E2E.Calendar.Data.Tests;

public sealed class ReminderRepositoryTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Due = new(2026, 10, 21, 9, 0, 0, TimeSpan.Zero);

    private readonly TempDatabase _database = new();
    private ReminderRepository _reminders = null!;

    public async Task InitializeAsync()
    {
        await _database.InitializeAsync();
        _reminders = new ReminderRepository(_database.Database);
    }

    public async Task DisposeAsync()
    {
        await _database.DisposeAsync();
    }

    private static Reminder CreateReminder(
        string id = "rem-1",
        DateTimeOffset? dueUtc = null,
        bool done = false,
        bool deleted = false) =>
        new()
        {
            Id = id,
            Title = "Pay the bill",
            Notes = "Reference number",
            DueUtc = dueUtc ?? Due,
            TimeZoneId = "Asia/Kolkata",
            RRule = "FREQ=MONTHLY",
            Priority = Priority.High,
            Flagged = true,
            DoneUtc = done ? new DateTimeOffset(2026, 10, 20, 12, 0, 0, TimeSpan.Zero) : null,
            ListId = "list-1",
            CreatedUtc = new DateTimeOffset(2026, 10, 19, 8, 0, 0, TimeSpan.Zero),
            UpdatedUtc = new DateTimeOffset(2026, 10, 19, 8, 0, 0, TimeSpan.Zero),
            Deleted = deleted,
            Dirty = true,
        };

    [Fact]
    public async Task A_reminder_round_trips_through_the_repository()
    {
        var original = CreateReminder();

        await _reminders.UpsertAsync(original);
        var loaded = await _reminders.GetByIdAsync(original.Id);

        loaded.Should().NotBeNull();
        loaded!.Title.Should().Be(original.Title);
        loaded.Notes.Should().Be(original.Notes);
        loaded.DueUtc.Should().Be(original.DueUtc);
        loaded.TimeZoneId.Should().Be(original.TimeZoneId);
        loaded.RRule.Should().Be(original.RRule);
        loaded.Priority.Should().Be(Priority.High);
        loaded.Flagged.Should().BeTrue();
        loaded.DoneUtc.Should().BeNull();
        loaded.ListId.Should().Be("list-1");
        loaded.Deleted.Should().BeFalse();
        loaded.Dirty.Should().BeTrue();
    }

    [Fact]
    public async Task A_range_query_returns_the_reminders_due_in_that_window()
    {
        await _reminders.UpsertAsync(CreateReminder(id: "inside", dueUtc: Due));
        await _reminders.UpsertAsync(CreateReminder(
            id: "outside",
            dueUtc: new DateTimeOffset(2026, 11, 21, 9, 0, 0, TimeSpan.Zero)));

        var results = await _reminders.GetInRangeAsync(
            new DateTimeOffset(2026, 10, 20, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 10, 22, 0, 0, 0, TimeSpan.Zero));

        results.Select(r => r.Id).Should().Equal("inside");
    }

    [Fact]
    public async Task Due_reminders_exclude_the_ones_that_are_already_done()
    {
        await _reminders.UpsertAsync(CreateReminder(id: "open"));
        await _reminders.UpsertAsync(CreateReminder(id: "done", done: true));

        var results = await _reminders.GetDueAsync(
            new DateTimeOffset(2026, 10, 21, 10, 0, 0, TimeSpan.Zero));

        results.Select(r => r.Id).Should().Equal("open");
    }

    [Fact]
    public async Task Upserting_a_reminder_updates_it_in_place()
    {
        await _reminders.UpsertAsync(CreateReminder());

        await _reminders.UpsertAsync(CreateReminder() with { Title = "Pay the electricity bill", Dirty = false });

        var loaded = await _reminders.GetByIdAsync("rem-1");

        loaded!.Title.Should().Be("Pay the electricity bill");
        loaded.Dirty.Should().BeFalse();
    }
}
