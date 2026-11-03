using System.Net.Http;
using E2E.Calendar.Core.Models;
using E2E.Calendar.Core.Services;
using E2E.Calendar.Sync.Ics;
using FluentAssertions;
using Xunit;

namespace E2E.Calendar.Sync.Tests;

public sealed class HolidayOverrideClientTests
{
    private const string PlaceholderUrl =
        "https://raw.githubusercontent.com/OWNER/REPO/main/holiday-overrides.json";

    [Theory]
    [InlineData(PlaceholderUrl)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-url")]
    public async Task Placeholder_url_skips_network_and_bundled_holidays_still_load(string url)
    {
        var handler = new CountingHandler();
        using var http = new HttpClient(handler);
        var client = new HolidayOverrideClient(http);

        string? json = null;
        Func<Task> act = async () => json = await client.TryDownloadAsync(url);

        await act.Should().NotThrowAsync();
        json.Should().BeNull();
        handler.Calls.Should().Be(0);

        var usHolidays = new IcsService().Import(
            File.ReadAllText(Path.Combine(HolidaysDirectory(), "us.ics")),
            "holiday-us",
            DateTimeOffset.UtcNow);
        var applied = HolidayOverrideApplier.ApplyOverrides(usHolidays, Array.Empty<HolidayOverride>());

        applied.Should().NotBeEmpty();
    }

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

    private sealed class CountingHandler : HttpMessageHandler
    {
        public int Calls { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Calls++;
            throw new InvalidOperationException("The network must not be called for a placeholder URL.");
        }
    }
}
