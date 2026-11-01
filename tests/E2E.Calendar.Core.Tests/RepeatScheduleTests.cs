using E2E.Calendar.Core.Services;
using FluentAssertions;
using Xunit;

namespace E2E.Calendar.Core.Tests;

public sealed class RepeatScheduleTests
{
    [Fact]
    public void Offers_once_and_every_five_ten_fifteen_thirty_minutes()
    {
        RepeatSchedule.Choices.Select(choice => choice.Minutes)
            .Should().Equal(null, 5, 10, 15, 30);

        RepeatSchedule.Choices.Select(choice => choice.Label)
            .Should().Equal("Once", "Every 5 min", "Every 10 min", "Every 15 min", "Every 30 min");
    }

    [Theory]
    [InlineData(null, "Once")]
    [InlineData(5, "Every 5 min")]
    [InlineData(10, "Every 10 min")]
    [InlineData(15, "Every 15 min")]
    [InlineData(30, "Every 30 min")]
    public void Maps_an_interval_back_to_its_option(int? minutes, string label)
    {
        RepeatSchedule.ForMinutes(minutes).Label.Should().Be(label);
    }

    [Fact]
    public void An_unknown_interval_falls_back_to_once()
    {
        RepeatSchedule.ForMinutes(7).Minutes.Should().BeNull();
    }

    [Theory]
    [InlineData(0, null)]
    [InlineData(1, 5)]
    [InlineData(2, 10)]
    [InlineData(3, 15)]
    [InlineData(4, 30)]
    [InlineData(-1, null)]
    [InlineData(5, null)]
    public void Maps_a_picker_index_to_its_interval(int index, int? minutes)
    {
        RepeatSchedule.MinutesAt(index).Should().Be(minutes);
    }
}
