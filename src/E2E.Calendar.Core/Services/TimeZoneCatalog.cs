using E2E.Calendar.Core;

namespace E2E.Calendar.Core.Services;

public static class TimeZoneCatalog
{
    public static IReadOnlyList<(string Id, string Label)> Common { get; } =
    [
        ("UTC", "UTC"),
        ("America/Los_Angeles", "Pacific Time"),
        ("America/Denver", "Mountain Time"),
        ("America/Chicago", "Central Time"),
        ("America/New_York", "Eastern Time"),
        ("America/Sao_Paulo", "Brasilia Time"),
        ("Europe/London", "London"),
        ("Europe/Paris", "Central European Time"),
        ("Africa/Cairo", "Eastern European Time"),
        ("Asia/Dubai", "Gulf Standard Time"),
        ("Asia/Karachi", "Pakistan Time"),
        ("Asia/Kolkata", "India Standard Time"),
        ("Asia/Singapore", "Singapore Time"),
        ("Asia/Tokyo", "Japan Time"),
        ("Australia/Sydney", "Australian Eastern Time"),
    ];

    public static int IndexOf(string? id)
    {
        if (!string.IsNullOrWhiteSpace(id))
        {
            var index = Common.ToList().FindIndex(item => string.Equals(item.Id, id, StringComparison.OrdinalIgnoreCase));
            if (index >= 0)
            {
                return index;
            }
        }

        var local = TimeZoneInfo.Local.ToIanaId();
        return Math.Max(0, Common.ToList().FindIndex(item => item.Id.Equals(local, StringComparison.OrdinalIgnoreCase)));
    }
}
