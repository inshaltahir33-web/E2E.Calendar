using E2E.Calendar.Core.Models;
using E2E.Calendar.Data.Repos;
using FluentAssertions;
using Xunit;
using CalendarDb = E2E.Calendar.Data.Db;

namespace E2E.Calendar.Data.Tests;

public sealed class AlertInstanceUpsertTests : IAsyncLifetime
{
    private readonly TempDatabase _database = new();

    private static readonly DateTimeOffset FireAt =
        new(2026, 11, 2, 9, 0, 0, TimeSpan.Zero);

    public async Task InitializeAsync() => await _database.InitializeAsync();

    public async Task DisposeAsync() => await _database.DisposeAsync();

    private AlertInstance Instance(
        string id,
        DateTimeOffset fireAt,
        AlertState state = AlertState.Pending,
        DateTimeOffset? snoozedUntilUtc = null,
        DateTimeOffset? occurrenceUtc = null,
        int repeatCount = 0) => new()
    {
        Id = id,
        AlertId = "alert-1",
        OwnerType = OwnerType.Reminder,
        OwnerId = "reminder-1",
        OccurrenceUtc = occurrenceUtc ?? fireAt,
        FireAtUtc = fireAt,
        State = state,
        SnoozedUntilUtc = snoozedUntilUtc,
        RepeatCount = repeatCount,
    };

    [Fact]
    public async Task UpsertInstance_keeps_fired_state_when_replanned()
    {
        var repo = new AlertRepository(_database.Database);
        var id = "alert-1:1000";

        await repo.UpsertInstanceAsync(Instance(id, FireAt));
        (await repo.TryMarkFiredAsync(id, FireAt)).Should().BeTrue();

        // The planner re-upserts the same instance on every run (same fire time).
        await repo.UpsertInstanceAsync(Instance(id, FireAt));
        var fired = await repo.GetInstanceByIdAsync(id);
        fired!.State.Should().Be(AlertState.Fired);

        // Even when the alert's trigger time changed, a fired instance stays fired.
        await repo.UpsertInstanceAsync(Instance(id, FireAt.AddMinutes(30)));
        var stillFired = await repo.GetInstanceByIdAsync(id);
        stillFired!.State.Should().Be(AlertState.Fired);
        stillFired.FiredUtc.Should().Be(FireAt);
    }

    [Fact]
    public async Task UpsertInstance_rearms_a_snoozed_instance_when_the_trigger_time_changed()
    {
        var repo = new AlertRepository(_database.Database);
        var id = "alert-1:2000";

        await repo.UpsertInstanceAsync(Instance(id, FireAt));
        await repo.MarkSnoozedAsync(id, FireAt.AddMinutes(10));

        // A re-plan with a changed fire time re-arms the instance as pending.
        await repo.UpsertInstanceAsync(Instance(id, FireAt.AddMinutes(30)));
        var rearmed = await repo.GetInstanceByIdAsync(id);
        rearmed!.State.Should().Be(AlertState.Pending);
        rearmed.FireAtUtc.Should().Be(FireAt.AddMinutes(30));
        rearmed.SnoozedUntilUtc.Should().BeNull();
    }

    [Fact]
    public async Task DeleteStalePendingInstances_removes_unplanned_instances_but_keeps_snoozed()
    {
        var repo = new AlertRepository(_database.Database);

        await repo.UpsertInstanceAsync(Instance("alert-1:1", FireAt));
        await repo.UpsertInstanceAsync(Instance("alert-1:2", FireAt.AddHours(1)));
        await repo.UpsertInstanceAsync(Instance("alert-1:2:snooze:99", FireAt.AddMinutes(10),
            snoozedUntilUtc: FireAt.AddMinutes(10)));

        await repo.DeleteStalePendingInstancesAsync("alert-1", ["alert-1:1"]);

        var kept = await repo.GetInstanceByIdAsync("alert-1:1");
        kept!.State.Should().Be(AlertState.Pending);

        var stale = await repo.GetInstanceByIdAsync("alert-1:2");
        stale.Should().BeNull(); // the unplanned pending instance was removed

        var snoozedCopy = await repo.GetInstanceByIdAsync("alert-1:2:snooze:99");
        snoozedCopy.Should().NotBeNull();
        snoozedCopy!.State.Should().Be(AlertState.Pending);
    }

    [Fact]
    public async Task DeleteStalePendingInstances_keeps_queued_repeats()
    {
        var repo = new AlertRepository(_database.Database);

        await repo.UpsertInstanceAsync(Instance("alert-1:1", FireAt));
        await repo.UpsertInstanceAsync(Instance(
            "alert-1:1:repeat:1",
            FireAt.AddMinutes(5),
            occurrenceUtc: FireAt,
            repeatCount: 1));

        await repo.DeleteStalePendingInstancesAsync("alert-1", ["alert-1:1"]);

        var repeat = await repo.GetInstanceByIdAsync("alert-1:1:repeat:1");
        repeat.Should().NotBeNull();
        repeat!.RepeatCount.Should().Be(1);
    }

    [Fact]
    public async Task CancelPendingRepeats_removes_the_queued_chain_but_not_other_occurrences()
    {
        var repo = new AlertRepository(_database.Database);

        await repo.UpsertInstanceAsync(Instance("alert-1:1", FireAt));
        await repo.UpsertInstanceAsync(Instance(
            "alert-1:1:repeat:1",
            FireAt.AddMinutes(5),
            occurrenceUtc: FireAt,
            repeatCount: 1));
        await repo.UpsertInstanceAsync(Instance(
            "alert-1:2:repeat:1",
            FireAt.AddHours(1).AddMinutes(5),
            occurrenceUtc: FireAt.AddHours(1),
            repeatCount: 1));

        await repo.CancelPendingRepeatsAsync("alert-1", FireAt);

        (await repo.GetInstanceByIdAsync("alert-1:1")).Should().NotBeNull();
        (await repo.GetInstanceByIdAsync("alert-1:1:repeat:1")).Should().BeNull();
        (await repo.GetInstanceByIdAsync("alert-1:2:repeat:1")).Should().NotBeNull();
    }
}
