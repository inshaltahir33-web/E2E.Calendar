using E2E.Calendar.Core.Services;
using FluentAssertions;
using Xunit;

namespace E2E.Calendar.Core.Tests;

public sealed class SnoozeScheduleTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 1, 14, 30, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(SnoozeChoice.FiveMinutes, 5)]
    [InlineData(SnoozeChoice.TenMinutes, 10)]
    [InlineData(SnoozeChoice.ThirtyMinutes, 30)]
    [InlineData(SnoozeChoice.OneHour, 60)]
    public void Minute_options_are_relative_to_now(SnoozeChoice choice, int minutes)
    {
        var fireAt = SnoozeSchedule.Resolve(choice, Now, TimeZoneInfo.Utc);

        fireAt.Should().Be(Now.AddMinutes(minutes));
    }

    [Fact]
    public void Tomorrow_at_nine_uses_local_nine_am_not_twenty_four_hours()
    {
        var timeZone = TimeZoneInfo.CreateCustomTimeZone("test+2", TimeSpan.FromHours(2), "test+2", "test+2");
        var nowLocal = new DateTimeOffset(2026, 11, 1, 23, 30, 0, TimeSpan.FromHours(2));
        var nowUtc = nowLocal.ToUniversalTime();

        var fireAt = SnoozeSchedule.Resolve(SnoozeChoice.TomorrowAtNine, nowUtc, timeZone);

        var expectedLocal = new DateTimeOffset(2026, 11, 2, 9, 0, 0, TimeSpan.FromHours(2));
        fireAt.Should().Be(expectedLocal.ToUniversalTime());
    }

    [Fact]
    public void Tomorrow_at_nine_returns_utc_and_keeps_the_local_wall_clock()
    {
        var fireAt = SnoozeSchedule.Resolve(SnoozeChoice.TomorrowAtNine, Now, TimeZoneInfo.Utc);

        fireAt.Should().Be(new DateTimeOffset(2026, 11, 2, 9, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public void Every_choice_has_a_unique_round_tripping_key_and_label()
    {
        SnoozeSchedule.Choices.Should().HaveCount(5);
        SnoozeSchedule.Choices.Select(SnoozeSchedule.ToKey).Should().OnlyHaveUniqueItems();
        SnoozeSchedule.Choices.Select(SnoozeSchedule.ToLabel).Should().OnlyHaveUniqueItems();

        foreach (var choice in SnoozeSchedule.Choices)
        {
            SnoozeSchedule.TryParse(SnoozeSchedule.ToKey(choice), out var parsed).Should().BeTrue();
            parsed.Should().Be(choice);
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("tomorrow")]
    [InlineData("15")]
    public void Unknown_keys_are_rejected(string? key)
    {
        SnoozeSchedule.TryParse(key, out _).Should().BeFalse();
    }
}
