using E2E.Calendar.Core;
using E2E.Calendar.Core.Models;
using FluentAssertions;
using Xunit;

namespace E2E.Calendar.Core.Tests;

public sealed class MeetingLinkDetectorTests
{
    [Fact]
    public void A_teams_meeting_link_is_detected()
    {
        var link = MeetingLinkDetector.Detect("https://teams.microsoft.com/l/meetup-join/19%3ameeting_abc123");

        link.Should().NotBeNull();
        link!.Provider.Should().Be(MeetingProvider.MicrosoftTeams);
        link.JoinLabel.Should().Be("Join Teams");
        link.Url.Should().StartWith("https://teams.microsoft.com/");
    }

    [Fact]
    public void A_zoom_meeting_link_is_detected()
    {
        var link = MeetingLinkDetector.Detect("https://zoom.us/j/1234567890");

        link.Should().NotBeNull();
        link!.Provider.Should().Be(MeetingProvider.Zoom);
        link.JoinLabel.Should().Be("Join Zoom");
    }

    [Fact]
    public void A_google_meet_link_is_detected()
    {
        var link = MeetingLinkDetector.Detect("https://meet.google.com/abc-defg-hij");

        link.Should().NotBeNull();
        link!.Provider.Should().Be(MeetingProvider.GoogleMeet);
        link.JoinLabel.Should().Be("Join Meet");
    }

    [Fact]
    public void A_meeting_link_is_found_inside_a_longer_text()
    {
        var link = MeetingLinkDetector.Detect(
            "Weekly sync, link: https://zoom.us/j/987654321 please join");

        link.Should().NotBeNull();
        link!.Provider.Should().Be(MeetingProvider.Zoom);
        link.Url.Should().Be("https://zoom.us/j/987654321");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("just a normal note")]
    [InlineData("https://example.invalid/meeting")]
    public void Text_without_a_meeting_link_is_not_detected(string? text)
    {
        MeetingLinkDetector.Detect(text).Should().BeNull();
    }

    [Theory]
    [InlineData("https://example.invalid/event", true)]
    [InlineData("http://example.invalid/event", true)]
    [InlineData("ftp://example.invalid/file", false)]
    [InlineData("javascript:alert(1)", false)]
    [InlineData("file:///C:/secret.txt", false)]
    [InlineData("not a url", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Only_http_and_https_links_are_allowed(string? url, bool allowed)
    {
        UrlPolicy.IsAllowed(url).Should().Be(allowed);
    }
}
