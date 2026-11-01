using System.Text.Json.Serialization;

namespace E2E.Calendar.Core.Models;

/// <summary>
/// A single holiday date override downloaded from the remote overrides feed.
/// Entries are matched against bundled holidays by country + name; matched
/// holidays are moved to <see cref="Date"/> and lose their "(approx.)" label.
/// </summary>
public sealed record HolidayOverride
{
    [JsonPropertyName("country")]
    public string Country { get; init; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("date")]
    public DateOnly Date { get; init; }

    public HolidayOverride()
    {
    }

    public HolidayOverride(string country, string name, DateOnly date)
    {
        Country = country;
        Name = name;
        Date = date;
    }

    public HolidayOverride(string country, string name, DateTime date)
        : this(country, name, DateOnly.FromDateTime(date))
    {
    }

    public HolidayOverride(string country, string name, DateTimeOffset date)
        : this(country, name, DateOnly.FromDateTime(date.DateTime))
    {
    }

    public HolidayOverride(string country, string name, string date)
        : this(country, name, DateOnly.Parse(date, System.Globalization.CultureInfo.InvariantCulture))
    {
    }
}