using System.Text.RegularExpressions;

namespace E2E.Calendar.Core.Models;

public enum MeetingProvider
{
    None = 0,
    MicrosoftTeams = 1,
    Zoom = 2,
    GoogleMeet = 3,
}

public sealed record MeetingLink(string Url, MeetingProvider Provider, string JoinLabel, string DisplayUrl);

public static partial class MeetingLinkDetector
{
    [GeneratedRegex(@"https?://(?:[a-z0-9-]+\.)*teams\.(?:microsoft\.com|live\.com)/(?:l/meetup-join|v\d)/[^\s<>""']+", RegexOptions.IgnoreCase)]
    private static partial Regex TeamsPattern();

    [GeneratedRegex(@"https?://(?:[a-z0-9-]+\.)*zoom\.(?:us|com)/(?:j|my|wc)/[^\s<>""']+", RegexOptions.IgnoreCase)]
    private static partial Regex ZoomPattern();

    [GeneratedRegex(@"https?://meet\.google\.com/[a-z0-9-]+", RegexOptions.IgnoreCase)]
    private static partial Regex GoogleMeetPattern();

    public static MeetingLink? Detect(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var candidate = text.Trim();

        var match = TeamsPattern().Match(candidate);

        if (match.Success)
        {
            return Create(match.Value, MeetingProvider.MicrosoftTeams, "Join Teams");
        }

        match = ZoomPattern().Match(candidate);

        if (match.Success)
        {
            return Create(match.Value, MeetingProvider.Zoom, "Join Zoom");
        }

        match = GoogleMeetPattern().Match(candidate);

        if (match.Success)
        {
            return Create(match.Value, MeetingProvider.GoogleMeet, "Join Meet");
        }

        return null;
    }

    private static MeetingLink Create(string url, MeetingProvider provider, string label) =>
        new(url, provider, label, url);
}
