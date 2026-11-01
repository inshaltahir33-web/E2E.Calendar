using E2E.Calendar.Sync.Ics;
using FluentAssertions;
using Xunit;

namespace E2E.Calendar.Sync.Tests;

public sealed class HolidayCalendarTests
{
    private static string HolidaysDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "src", "E2E.Calendar.App", "Resources", "Holidays");

            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the bundled holiday resources.");
    }

    [Theory]
    [InlineData("pk.ics")]
    [InlineData("us.ics")]
    [InlineData("uk.ics")]
    [InlineData("ae.ics")]
    [InlineData("in.ics")]
    public void Every_country_has_holidays_in_each_year_2026_to_2030(string fileName)
    {
        var path = Path.Combine(HolidaysDirectory(), fileName);
        File.Exists(path).Should().BeTrue();

        var content = File.ReadAllText(path);

        foreach (var year in Enumerable.Range(2026, 5))
        {
            content.Should().Contain($"DTSTART;VALUE=DATE:{year}");
        }
    }

    [Fact]
    public void Pakistan_holidays_include_independence_day_2027()
    {
        var path = Path.Combine(HolidaysDirectory(), "pk.ics");
        var content = File.ReadAllText(path);
        var service = new IcsService();
        var events = service.Import(content, "holiday-pk", DateTimeOffset.UtcNow);

        events.Should().Contain(item =>
            item.StartUtc.Month == 8 &&
            item.StartUtc.Day == 14 &&
            item.StartUtc.Year == 2027);
    }
}