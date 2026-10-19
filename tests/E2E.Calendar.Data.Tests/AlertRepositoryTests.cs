using E2E.Calendar.Core.Models;
using E2E.Calendar.Data.Repos;
using FluentAssertions;
using Xunit;

namespace E2E.Calendar.Data.Tests;

public sealed class AlertRepositoryTests : IAsyncLifetime
{
    private static readonly DateTimeOffset FireAt = new(2026, 10, 21, 8, 30, 0, TimeSpan.Zero);

    private readonly TempDatabase _database = new();
    private AlertRepository _alerts = null!;

    public async Task InitializeAsync()
    {
        await _database.InitializeAsync();
        _alerts = new AlertRepository(_database.Database);
    }

    public async Task DisposeAsync()
    {
        await _database.DisposeAsync();
    }

    private static Alert CreateAlert(string id = "alert-1", OwnerType ownerType = OwnerType.Event) =>
        new()
        {
            Id = id,
            OwnerType = ownerType,
            OwnerId = "evt-1",
            TriggerOffsetMinutes = 30,
            TriggerAtUtc = null,
            Priority = Priority.Critical,
            Persistent = true,
            RepeatEveryMinutes = 5,
            Sound = "chime",
        };

    private static AlertInstance CreateInstance(
        string id = "instance-1",
        DateTimeOffset? fireAtUtc = null,
        AlertState state = AlertState.Pending) =>
        new()
        {
            Id = id,
            AlertId = "alert-1",
            OwnerType = OwnerType.Event,
            OwnerId = "evt-1",
            OccurrenceUtc = new DateTimeOffset(2026, 10, 21, 9, 0, 0, TimeSpan.Zero),
            FireAtUtc = fireAtUtc ?? FireAt,
            State = state,
            SnoozedUntilUtc = null,
            FiredUtc = null,
            RepeatCount = 0,
        };

    [Fact]
    public async Task An_alert_round_trips_through_the_repository()
    {
        var original = CreateAlert();

        await _alerts.UpsertAlertAsync(original);
        var loaded = await _alerts.GetAlertByIdAsync(original.Id);

        loaded.Should().NotBeNull();
        loaded!.OwnerType.Should().Be(OwnerType.Event);
        loaded.OwnerId.Should().Be("evt-1");
        loaded.TriggerOffsetMinutes.Should().Be(30);
        loaded.TriggerAtUtc.Should().BeNull();
        loaded.Priority.Should().Be(Priority.Critical);
        loaded.Persistent.Should().BeTrue();
        loaded.RepeatEveryMinutes.Should().Be(5);
        loaded.Sound.Should().Be("chime");
    }

    [Fact]
    public async Task Alerts_can_be_listed_for_an_owner()
    {
        await _alerts.UpsertAlertAsync(CreateAlert(id: "one"));
        await _alerts.UpsertAlertAsync(CreateAlert(id: "two"));
        await _alerts.UpsertAlertAsync(CreateAlert(id: "other", ownerType: OwnerType.Reminder));

        var results = await _alerts.GetAlertsForOwnerAsync(OwnerType.Event, "evt-1");

        results.Select(a => a.Id).Should().BeEquivalentTo(["one", "two"]);
    }

    [Fact]
    public async Task An_alert_can_be_deleted()
    {
        await _alerts.UpsertAlertAsync(CreateAlert());

        await _alerts.DeleteAlertAsync("alert-1");

        var loaded = await _alerts.GetAlertByIdAsync("alert-1");
        loaded.Should().BeNull();
    }

    [Fact]
    public async Task An_alert_instance_round_trips_through_the_repository()
    {
        var original = CreateInstance();

        await _alerts.UpsertInstanceAsync(original);
        var loaded = await _alerts.GetInstanceByIdAsync(original.Id);

        loaded.Should().NotBeNull();
        loaded!.AlertId.Should().Be("alert-1");
        loaded.OwnerType.Should().Be(OwnerType.Event);
        loaded.OccurrenceUtc.Should().Be(original.OccurrenceUtc);
        loaded.FireAtUtc.Should().Be(original.FireAtUtc);
        loaded.State.Should().Be(AlertState.Pending);
        loaded.RepeatCount.Should().Be(0);
    }

    [Fact]
    public async Task Only_pending_instances_that_are_due_are_returned()
    {
        await _alerts.UpsertInstanceAsync(CreateInstance(id: "due"));
        await _alerts.UpsertInstanceAsync(CreateInstance(
            id: "later",
            fireAtUtc: new DateTimeOffset(2026, 10, 21, 10, 0, 0, TimeSpan.Zero)));
        await _alerts.UpsertInstanceAsync(CreateInstance(id: "fired", state: AlertState.Fired));

        var results = await _alerts.GetPendingInstancesAsync(
            new DateTimeOffset(2026, 10, 21, 9, 0, 0, TimeSpan.Zero));

        results.Select(i => i.Id).Should().Equal("due");
    }

    [Fact]
    public async Task The_next_pending_instance_is_the_earliest_one()
    {
        await _alerts.UpsertInstanceAsync(CreateInstance(
            id: "later",
            fireAtUtc: new DateTimeOffset(2026, 10, 21, 10, 0, 0, TimeSpan.Zero)));
        await _alerts.UpsertInstanceAsync(CreateInstance(id: "sooner"));

        var next = await _alerts.PeekNextPendingInstanceAsync();

        next.Should().NotBeNull();
        next!.Id.Should().Be("sooner");
    }
}
