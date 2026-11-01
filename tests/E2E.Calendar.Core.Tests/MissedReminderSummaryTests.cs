using E2E.Calendar.Core.Models;
using E2E.Calendar.Core.Services;
using FluentAssertions;
using Xunit;

namespace E2E.Calendar.Core.Tests;

public sealed class MissedReminderSummaryTests
{
    [Fact]
    public void Several_instances_of_one_reminder_count_once()
    {
        var missed = new[]
        {
            Instance("alert-1", "reminder-1", repeatCount: 0),
            Instance("alert-1", "reminder-1", repeatCount: 1),
            Instance("alert-2", "reminder-1", repeatCount: 0),
        };

        MissedReminderSummary.Count(missed).Should().Be(1);
    }

    [Fact]
    public void Different_owners_are_counted_separately()
    {
        var missed = new[]
        {
            Instance("alert-1", "reminder-1", repeatCount: 0),
            Instance("alert-2", "task-1", repeatCount: 0, ownerType: OwnerType.Task),
            Instance("alert-3", "event-1", repeatCount: 0, ownerType: OwnerType.Event),
        };

        MissedReminderSummary.Count(missed).Should().Be(3);
    }

    [Fact]
    public void An_owner_id_reused_by_two_owner_types_counts_twice()
    {
        var missed = new[]
        {
            Instance("alert-1", "shared-id", repeatCount: 0),
            Instance("alert-2", "shared-id", repeatCount: 0, ownerType: OwnerType.Task),
        };

        MissedReminderSummary.Count(missed).Should().Be(2);
    }

    [Fact]
    public void Nothing_missed_counts_zero()
    {
        MissedReminderSummary.Count([]).Should().Be(0);
    }

    [Theory]
    [InlineData(1, "1 missed reminder")]
    [InlineData(2, "2 missed reminders")]
    [InlineData(12, "12 missed reminders")]
    public void The_title_pluralises_the_count(int count, string expected)
    {
        MissedReminderSummary.Title(count).Should().Be(expected);
    }

    private static AlertInstance Instance(
        string alertId,
        string ownerId,
        int repeatCount,
        OwnerType ownerType = OwnerType.Reminder) => new()
    {
        Id = repeatCount == 0 ? $"{alertId}:1000" : $"{alertId}:1000:repeat:{repeatCount}",
        AlertId = alertId,
        OwnerType = ownerType,
        OwnerId = ownerId,
        OccurrenceUtc = new DateTimeOffset(2026, 11, 1, 8, 0, 0, TimeSpan.Zero),
        FireAtUtc = new DateTimeOffset(2026, 11, 1, 8, 0, 0, TimeSpan.Zero),
        State = AlertState.Pending,
        RepeatCount = repeatCount,
    };
}
