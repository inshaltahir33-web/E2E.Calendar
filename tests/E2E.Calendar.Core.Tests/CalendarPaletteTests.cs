using E2E.Calendar.Core.Models;
using FluentAssertions;
using Xunit;

namespace E2E.Calendar.Core.Tests;

public sealed class CalendarPaletteTests
{
    [Theory]
    [InlineData("Indigo", "Indigo")]
    [InlineData("blue", "Blue")]
    [InlineData("  tEaL  ", "Teal")]
    [InlineData("Brown", "Brown")]
    public void Normalize_accepts_palette_names_case_insensitively(string input, string expected)
    {
        CalendarPalette.Normalize(input).Should().Be(expected);
    }

    [Theory]
    [InlineData("#0078D4", "Blue")]
    [InlineData("#8764B8", "Purple")]
    [InlineData("#C239B3", "Pink")]
    [InlineData("#00B7C3", "Teal")]
    [InlineData("#FF8C00", "Orange")]
    [InlineData("#00B050", "Green")]
    [InlineData("#E3008C", "Pink")]
    [InlineData("#986F0B", "Amber")]
    [InlineData("#004E8C", "Blue")]
    [InlineData("#E81123", "Red")]
    [InlineData("#6A00C4", "Purple")]
    [InlineData("#767676", "Slate")]
    public void Normalize_maps_legacy_hex_values_to_the_nearest_palette_name(string legacy, string expected)
    {
        CalendarPalette.Normalize(legacy).Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("#123456")]
    [InlineData("not-a-colour")]
    public void Normalize_falls_back_to_slate_never_grey(string? value)
    {
        CalendarPalette.Normalize(value).Should().Be(CalendarPalette.Slate);
    }

    [Fact]
    public void The_palette_has_the_twelve_theme_colours_in_order()
    {
        CalendarPalette.All.Should().Equal(
            "Indigo",
            "Blue",
            "Teal",
            "Green",
            "Lime",
            "Amber",
            "Orange",
            "Red",
            "Pink",
            "Purple",
            "Slate",
            "Brown");
    }

    [Fact]
    public void Default_seeded_calendars_use_indigo_blue_and_pink()
    {
        CalendarPalette.Work.Should().Be("Indigo");
        CalendarPalette.Personal.Should().Be("Blue");
        CalendarPalette.Family.Should().Be("Pink");
    }
}
