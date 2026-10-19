using E2E.Calendar.Core.Models;
using E2E.Calendar.Data.Repos;
using FluentAssertions;
using Xunit;

namespace E2E.Calendar.Data.Tests;

public sealed class AlertRepositoryTests : IAsyncLifetime
{
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

    private static Alert CreateAlert(
        string id = "alert-1",
        OwnerType ownerType = OwnerType.Event,
        string ownerId = "evt-1",
        int? offsetMinutes = 15) =>
        new()
        {
            Id = id,
            OwnerType = ownerType,
            OwnerId = ownerId,
            TriggerOffsetMinutes = offsetMinutes,
            TriggerAtUtc = null,
            Priority = Priority.High,
            Persistent = false,
            RepeatEveryMinutes = null,
            Sound = "chime",
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
        loaded.TriggerOffsetMinutes.Should().Be(15);
        loaded.Priority.Should().Be(Priority.High);
        loaded.Sound.Should().Be("chime");
    }

    [Fact]
    public async Task The_alerts_of_an_owner_can_be_listed()
    {
        await _alerts.UpsertAlertAsync(CreateAlert(id: "one"));
        await _alerts.UpsertAlertAsync(CreateAlert(id: "two", offsetMinutes: 30));
        await _alerts.UpsertAlertAsync(CreateAlert(id: "other", ownerId: "evt-2"));

        var alerts = await _alerts.GetAlertsForOwnerAsync(OwnerType.Event, "evt-1");

        alerts.Select(alert => alert.Id).Should().BeEquivalentTo(["one", "two"]);
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
        var instance = new AlertInstance
        {
            Id = "instance-1",
            AlertId = "alert-1",
            OwnerType = OwnerType.Event,
            OwnerId = "evt-1",
            OccurrenceUtc = new DateTimeOffset(2026, 10, 21, 9, 0, 0, TimeSpan.Zero),
            FireAtUtc = new DateTimeOffset(2026, 10, 21, 8, 45, 0, TimeSpan.Zero),
            State = AlertState.Pending,
        };

        await _alerts.UpsertInstanceAsync(instance);
        var loaded = await _alerts.GetInstanceByIdAsync(instance.Id);

        loaded.Should().NotBeNull();
        loaded!.State.Should().Be(AlertState.Pending);
        loaded.FireAtUtc.Should().Be(instance.FireAtUtc);
        loaded.RepeatCount.Should().Be(0);
    }
}
