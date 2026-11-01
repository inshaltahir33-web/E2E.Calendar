using E2E.Calendar.Core.Models;
using E2E.Calendar.Core.Services;
using FluentAssertions;
using Xunit;

namespace E2E.Calendar.Core.Tests;

public sealed class HolidayOverrideTests
{
    [Fact]
    public void Apply_override_moves_date_and_drops_approx_label()
    {
        var holiday = new CalendarEvent
        {
            Id = "holiday-pk:eid",
            CalendarId = "holiday-pk",
            Title = "Eid-ul-Fitr (approx.)",
            StartUtc = new DateTimeOffset(2026, 3, 20, 0, 0, 0, TimeSpan.Zero),
            EndUtc = new DateTimeOffset(2026, 3, 21, 0, 0, 0, TimeSpan.Zero),
            AllDay = true,
            TimeZoneId = "UTC",
            CreatedUtc = DateTimeOffset.UtcNow,
            UpdatedUtc = DateTimeOffset.UtcNow,
        };

        var overrides = new[] { new HolidayOverride("pk", "Eid-ul-Fitr", new DateOnly(2026, 3, 21)) };

        var result = HolidayOverrideApplier.Apply(new[] { holiday }, overrides);

        result.Should().ContainSingle();
        result[0].Title.Should().Be("Eid-ul-Fitr");
        result[0].StartUtc.Date.Should().Be(new DateTime(2026, 3, 21));
        result[0].EndUtc.Date.Should().Be(new DateTime(2026, 3, 22));
    }

    [Fact]
    public void Apply_without_matching_override_keeps_original()
    {
        var holiday = new CalendarEvent
        {
            Id = "holiday-pk:labour",
            CalendarId = "holiday-pk",
            Title = "Labour Day",
            StartUtc = new DateTimeOffset(2026, 5, 1, 0, 0, 0, TimeSpan.Zero),
            EndUtc = new DateTimeOffset(2026, 5, 2, 0, 0, 0, TimeSpan.Zero),
            AllDay = true,
            TimeZoneId = "UTC",
            CreatedUtc = DateTimeOffset.UtcNow,
            UpdatedUtc = DateTimeOffset.UtcNow,
        };

        var result = HolidayOverrides.ApplyOverrides(new[] { holiday }, Array.Empty<HolidayOverride>());

        result[0].Title.Should().Be("Labour Day");
        result[0].StartUtc.Should().Be(holiday.StartUtc);
    }
}