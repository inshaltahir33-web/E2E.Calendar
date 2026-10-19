using E2E.Calendar.Core.Models;
using E2E.Calendar.Core.Services;
using FluentAssertions;
using Xunit;

namespace E2E.Calendar.Core.Tests;

public sealed class OverlapLayoutTests
{
    private static readonly DateTimeOffset DayStart = new(2026, 10, 21, 0, 0, 0, TimeSpan.Zero);

    private static CalendarItem Item(
        string id,
        int startHour,
        int endHour,
        DateTimeOffset? day = null) =>
        new()
        {
            Id = id,
            Title = id,
            StartUtc = (day ?? DayStart).AddHours(startHour),
            EndUtc = (day ?? DayStart).AddHours(endHour),
        };

    [Fact]
    public void Items_that_do_not_overlap_share_a_single_column()
    {
        var packed = OverlapLayout.Pack(
        [
            Item("a", 9, 10),
            Item("b", 11, 12),
            Item("c", 13, 14),
        ]);

        packed.Should().HaveCount(3);
        packed.Should().OnlyContain(entry => entry.Column == 0);
        packed.Should().OnlyContain(entry => entry.ColumnCount == 1);
    }

    [Fact]
    public void Overlapping_items_are_packed_into_separate_columns()
    {
        var packed = OverlapLayout.Pack(
        [
            Item("a", 9, 11),
            Item("b", 10, 12),
        ]);

        packed.Should().HaveCount(2);
        packed.Select(entry => entry.Column).Should().BeEquivalentTo([0, 1]);
        packed.Should().OnlyContain(entry => entry.ColumnCount == 2);
    }

    [Fact]
    public void An_item_that_does_not_overlap_the_first_one_reuses_its_column()
    {
        var packed = OverlapLayout.Pack(
        [
            Item("a", 9, 11),
            Item("b", 10, 12),
            Item("c", 11, 13),
        ]);

        var byId = packed.ToDictionary(entry => entry.Item.Id);

        byId["a"].Column.Should().Be(0);
        byId["b"].Column.Should().Be(1);
        byId["c"].Column.Should().Be(0);
        byId["a"].ColumnCount.Should().Be(2);
    }

    [Fact]
    public void Items_in_separate_clusters_get_their_own_column_count()
    {
        var packed = OverlapLayout.Pack(
        [
            Item("a", 9, 10),
            Item("b", 10, 11),
            Item("c", 14, 16),
            Item("d", 15, 17),
        ]);

        var byId = packed.ToDictionary(entry => entry.Item.Id);

        byId["a"].ColumnCount.Should().Be(1);
        byId["b"].ColumnCount.Should().Be(1);
        byId["c"].ColumnCount.Should().Be(2);
        byId["d"].ColumnCount.Should().Be(2);
    }

    [Fact]
    public void An_item_that_ends_exactly_when_the_next_one_starts_does_not_overlap_it()
    {
        var packed = OverlapLayout.Pack(
        [
            Item("a", 9, 10),
            Item("b", 10, 11),
        ]);

        packed.Should().OnlyContain(entry => entry.Column == 0);
    }

    [Fact]
    public void Fractions_describe_the_position_inside_the_visible_range()
    {
        var packed = OverlapLayout.Pack(
        [Item("a", 6, 12)],
            DayStart,
            DayStart.AddHours(24));

        packed.Single().StartFraction.Should().Be(0.25);
        packed.Single().EndFraction.Should().Be(0.5);
    }

    [Fact]
    public void Fractions_are_clamped_to_the_visible_range()
    {
        var packed = OverlapLayout.Pack(
        [
            Item("before", 0, 2),
            Item("after", 22, 23),
        ],
            DayStart.AddHours(8),
            DayStart.AddHours(20));

        var byId = packed.ToDictionary(entry => entry.Item.Id);

        byId["before"].StartFraction.Should().Be(0);
        byId["before"].EndFraction.Should().Be(0);
        byId["after"].StartFraction.Should().Be(1);
        byId["after"].EndFraction.Should().Be(1);
    }

    [Fact]
    public void Without_a_range_the_fractions_stay_at_zero()
    {
        var packed = OverlapLayout.Pack([Item("a", 9, 10)]);

        packed.Single().StartFraction.Should().Be(0);
        packed.Single().EndFraction.Should().Be(0);
    }

    [Fact]
    public void Items_without_a_duration_are_ignored()
    {
        var packed = OverlapLayout.Pack(
        [
            Item("empty", 9, 9),
            Item("a", 9, 10),
        ]);

        packed.Should().HaveCount(1);
        packed.Single().Item.Id.Should().Be("a");
    }

    [Fact]
    public void The_result_is_ordered_by_start_time()
    {
        var packed = OverlapLayout.Pack(
        [
            Item("c", 13, 14),
            Item("a", 9, 10),
            Item("b", 11, 12),
        ]);

        packed.Select(entry => entry.Item.Id).Should().Equal("a", "b", "c");
    }

    [Fact]
    public void Three_way_overlaps_use_three_columns()
    {
        var packed = OverlapLayout.Pack(
        [
            Item("a", 9, 12),
            Item("b", 10, 13),
            Item("c", 11, 14),
        ]);

        packed.Select(entry => entry.Column).Should().BeEquivalentTo([0, 1, 2]);
        packed.Should().OnlyContain(entry => entry.ColumnCount == 3);
    }
}
