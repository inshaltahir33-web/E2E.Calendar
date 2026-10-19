using E2E.Calendar.Core;
using E2E.Calendar.Core.Entitlements;
using E2E.Calendar.Core.Models;
using FluentAssertions;
using Xunit;

namespace E2E.Calendar.Core.Tests;

public sealed class ModelContractTests
{
    [Fact]
    public void Priority_is_ordered_from_low_to_critical()
    {
        ((int)Priority.Low).Should().Be(0);
        ((int)Priority.Normal).Should().Be(1);
        ((int)Priority.High).Should().Be(2);
        ((int)Priority.Critical).Should().Be(3);
    }

    [Fact]
    public void AlertState_covers_every_state_of_the_alert_instance_lifecycle()
    {
        Enum.GetValues<AlertState>().Should().BeEquivalentTo(
        [
            AlertState.Pending,
            AlertState.Fired,
            AlertState.Snoozed,
            AlertState.Done,
            AlertState.Missed,
            AlertState.Dismissed,
        ]);
    }

    [Fact]
    public void Provider_covers_every_sync_provider()
    {
        Enum.GetValues<Provider>().Should().BeEquivalentTo(
        [
            Provider.Local,
            Provider.Google,
            Provider.Microsoft,
            Provider.CalDav,
            Provider.Ics,
        ]);
    }

    [Fact]
    public void OwnerType_covers_every_alert_owner()
    {
        Enum.GetValues<OwnerType>().Should().BeEquivalentTo(
        [
            OwnerType.Event,
            OwnerType.Reminder,
            OwnerType.Task,
        ]);
    }

    [Fact]
    public void A_new_event_is_not_dirty_and_not_deleted_by_default()
    {
        var calendarEvent = new CalendarEvent
        {
            Id = "evt-1",
            CalendarId = "cal-local",
            Title = "Standup",
            StartUtc = DateTimeOffset.UnixEpoch,
            EndUtc = DateTimeOffset.UnixEpoch,
            TimeZoneId = "Asia/Kolkata",
            CreatedUtc = DateTimeOffset.UnixEpoch,
            UpdatedUtc = DateTimeOffset.UnixEpoch,
        };

        calendarEvent.Dirty.Should().BeFalse();
        calendarEvent.Deleted.Should().BeFalse();
    }

    [Fact]
    public void A_new_alert_instance_starts_out_pending()
    {
        var instance = new AlertInstance
        {
            Id = "instance-1",
            AlertId = "alert-1",
            OwnerType = OwnerType.Event,
            OwnerId = "evt-1",
            OccurrenceUtc = DateTimeOffset.UnixEpoch,
            FireAtUtc = DateTimeOffset.UnixEpoch,
        };

        instance.State.Should().Be(AlertState.Pending);
        instance.RepeatCount.Should().Be(0);
    }

    [Fact]
    public void Free_tier_limits_match_the_specification()
    {
        FreeTier.LimitFor(Feature.MultipleAccounts).Should().Be(1);
        FreeTier.LimitFor(Feature.MultipleTaskLists).Should().Be(1);
        FreeTier.LimitFor(Feature.UnlimitedAlerts).Should().Be(3);
        FreeTier.LimitFor(Feature.CalendarTemplates).Should().Be(2);
    }

    [Fact]
    public void ToIanaId_converts_a_windows_time_zone_id_to_an_iana_id()
    {
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById("India Standard Time");

        var ianaId = timeZone.ToIanaId();

        ianaId.Should().NotBe(timeZone.Id);
        ianaId.Should().Contain("/");
        ianaId.ToTimeZoneInfo().BaseUtcOffset.Should().Be(timeZone.BaseUtcOffset);
    }

    [Fact]
    public void ToIanaId_keeps_an_iana_id_unchanged()
    {
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata");

        timeZone.ToIanaId().Should().Be("Asia/Kolkata");
    }

    [Fact]
    public void TryToTimeZoneInfo_resolves_a_known_iana_id()
    {
        "Asia/Kolkata".TryToTimeZoneInfo(out var timeZone).Should().BeTrue();

        timeZone.Should().NotBeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Not/AZone")]
    public void TryToTimeZoneInfo_rejects_an_unknown_iana_id(string? ianaId)
    {
        ianaId.TryToTimeZoneInfo(out var timeZone).Should().BeFalse();

        timeZone.Should().Be(TimeZoneInfo.Utc);
    }
}
