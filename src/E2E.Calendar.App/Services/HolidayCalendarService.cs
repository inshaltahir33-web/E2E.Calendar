using System.Net.Http;
using System.Text.Json;
using E2E.Calendar.Core.Abstractions;
using E2E.Calendar.Core.Models;
using E2E.Calendar.Core.Services;
using E2E.Calendar.Sync.Ics;
using CalendarModel = E2E.Calendar.Core.Models.Calendar;

namespace E2E.Calendar.App.Services;

/// <summary>
/// Installs read-only, bundled holiday calendars (one per country) from embedded
/// ICS resources so the app works fully offline. Remote holiday overrides can refine
/// bundled (approximate) dates; failures fall back to bundled data.
/// </summary>
public sealed class HolidayCalendarService
{
    public sealed record Country(string Code, string Name, string ResourceName);

    public static IReadOnlyList<Country> Countries { get; } =
    [
        new("pk", "Pakistan", "pk.ics"),
        new("us", "United States", "us.ics"),
        new("uk", "United Kingdom", "uk.ics"),
        new("ae", "United Arab Emirates", "ae.ics"),
        new("in", "India", "in.ics"),
    ];

    private static readonly TimeSpan DownloadTimeout = TimeSpan.FromSeconds(5);

    private readonly ICalendarRepository _calendars;
    private readonly CalendarService _calendarCatalog;
    private readonly IEventRepository _events;
    private readonly IcsService _ics;
    private readonly IClock _clock;
    private readonly IEventQueryCache _cache;
    private readonly HttpClient _httpClient;

    public HolidayCalendarService(
        ICalendarRepository calendars,
        CalendarService calendarCatalog,
        IEventRepository events,
        IcsService ics,
        IClock clock,
        IEventQueryCache cache,
        HttpClient httpClient)
    {
        _calendars = calendars;
        _calendarCatalog = calendarCatalog;
        _events = events;
        _ics = ics;
        _clock = clock;
        _cache = cache;
        _httpClient = httpClient;
    }

    public static string CalendarId(string countryCode) => $"holiday-{countryCode}";

    public async Task<bool> IsEnabledAsync(string countryCode, CancellationToken cancellationToken = default) =>
        await _calendars.GetByIdAsync(CalendarId(countryCode), cancellationToken) is not null;

    public async Task<IReadOnlyList<HolidayOverride>> GetOverridesAsync(CancellationToken cancellationToken = default)
    {
        var cachePath = CachePath();
        var markerPath = MarkerPath();
        var today = _clock.UtcNow.Date;

        var shouldDownload = true;
        if (File.Exists(cachePath) && File.Exists(markerPath))
        {
            try
            {
                var lastText = await File.ReadAllTextAsync(markerPath, cancellationToken);
                if (DateOnly.TryParse(lastText, out var last) && last == DateOnly.FromDateTime(today))
                {
                    shouldDownload = false;
                }
            }
            catch
            {
                // Fall through to a download attempt.
            }
        }

        if (shouldDownload)
        {
            var json = await TryDownloadAsync(cancellationToken);
            var parsed = json is null ? null : TryParseOverrides(json);
            if (parsed is not null)
            {
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(cachePath)!);
                    await File.WriteAllTextAsync(cachePath, json, cancellationToken);
                    await File.WriteAllTextAsync(markerPath, today.ToString("yyyy-MM-dd"), cancellationToken);
                }
                catch
                {
                    // Caching is best-effort.
                }

                return parsed;
            }
        }

        if (File.Exists(cachePath))
        {
            try
            {
                var cachedJson = await File.ReadAllTextAsync(cachePath, cancellationToken);
                var cached = TryParseOverrides(cachedJson);
                if (cached is not null)
                {
                    return cached;
                }
            }
            catch
            {
                // Ignore cache read failures.
            }
        }

        return Array.Empty<HolidayOverride>();
    }

    public async Task EnableAsync(string countryCode, CancellationToken cancellationToken = default)
    {
        var country = Countries.First(item => string.Equals(item.Code, countryCode, StringComparison.OrdinalIgnoreCase));
        var calendar = new CalendarModel
        {
            Id = CalendarId(country.Code),
            Name = $"{country.Name} holidays",
            Color = CalendarPalette.Holiday,
            Visible = true,
            ReadOnly = true,
            IsLocal = true,
        };

        await _calendarCatalog.UpsertAsync(calendar, cancellationToken);

        var ics = ReadResource(country.ResourceName);
        var now = _clock.UtcNow;
        var overrides = await GetOverridesAsync(cancellationToken);

        var events = _ics.Import(ics, calendar.Id, now)
            .Select(item => item with
            {
                Id = $"{calendar.Id}:{item.Id}",
                CalendarId = calendar.Id,
                Color = CalendarPalette.Holiday,
                Dirty = false,
            })
            .ToList();

        var applied = HolidayOverrideApplier.Apply(events, overrides, country.Code);

        await _events.DeleteByCalendarIdsAsync([calendar.Id], cancellationToken);

        foreach (var calendarEvent in applied)
        {
            await _events.UpsertAsync(calendarEvent, cancellationToken);
        }

        _cache.Invalidate();
    }

    public async Task DisableAsync(string countryCode, CancellationToken cancellationToken = default)
    {
        var id = CalendarId(countryCode);
        await _events.DeleteByCalendarIdsAsync([id], cancellationToken);
        try
        {
            await _calendarCatalog.DeleteAsync(id, cancellationToken);
        }
        catch (InvalidOperationException)
        {
            // Already removed; nothing to do.
        }

        _cache.Invalidate();
    }

    public async Task RefreshEnabledAsync(CancellationToken cancellationToken = default)
    {
        // Force a fresh download by clearing the daily marker.
        try
        {
            if (File.Exists(MarkerPath()))
            {
                File.Delete(MarkerPath());
            }
        }
        catch
        {
            // Best effort.
        }

        foreach (var country in Countries)
        {
            if (await IsEnabledAsync(country.Code, cancellationToken))
            {
                await EnableAsync(country.Code, cancellationToken);
            }
        }
    }

    public async Task SetEnabledAsync(string countryCode, bool enabled, CancellationToken cancellationToken = default)
    {
        if (enabled)
        {
            await EnableAsync(countryCode, cancellationToken);
        }
        else
        {
            await DisableAsync(countryCode, cancellationToken);
        }
    }

    private async Task<string?> TryDownloadAsync(CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(AppFeatures.HolidayOverridesUrl, UriKind.Absolute, out var uri)
            || uri.Scheme is not ("https" or "http"))
        {
            return null;
        }

        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(DownloadTimeout);
            using var response = await _httpClient.GetAsync(uri, timeoutCts.Token);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync(timeoutCts.Token);
        }
        catch
        {
            return null;
        }
    }

    private static IReadOnlyList<HolidayOverride>? TryParseOverrides(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            JsonElement arrayElement;

            if (root.ValueKind == JsonValueKind.Array)
            {
                arrayElement = root;
            }
            else if (root.ValueKind == JsonValueKind.Object)
            {
                if (!TryGetArrayProperty(root, out arrayElement))
                {
                    return null;
                }
            }
            else
            {
                return null;
            }

            var result = new List<HolidayOverride>();
            foreach (var item in arrayElement.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object)
                {
                    return null;
                }

                if (!item.TryGetPropertyCaseInsensitive("country", out var countryElement)
                    || !item.TryGetPropertyCaseInsensitive("name", out var nameElement)
                    || !item.TryGetPropertyCaseInsensitive("date", out var dateElement))
                {
                    return null;
                }

                var country = countryElement.GetString();
                var name = nameElement.GetString();
                if (string.IsNullOrWhiteSpace(country) || string.IsNullOrWhiteSpace(name))
                {
                    return null;
                }

                DateOnly date;
                if (dateElement.ValueKind == JsonValueKind.String)
                {
                    if (!DateOnly.TryParse(dateElement.GetString(), out date))
                    {
                        return null;
                    }
                }
                else
                {
                    return null;
                }

                result.Add(new HolidayOverride(country!, name!, date));
            }

            return result;
        }
        catch
        {
            return null;
        }
    }

    private static bool TryGetArrayProperty(JsonElement root, out JsonElement arrayElement)
    {
        foreach (var propertyName in new[] { "overrides", "entries", "items", "data" })
        {
            if (root.TryGetProperty(propertyName, out var candidate) && candidate.ValueKind == JsonValueKind.Array)
            {
                arrayElement = candidate;
                return true;
            }
        }

        arrayElement = default;
        return false;
    }

    private static string CachePath() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "E2E.Calendar",
            "holiday-overrides.json");

    private static string MarkerPath() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "E2E.Calendar",
            "holiday-overrides.last");

    private static string ReadResource(string fileName)
    {
        var assembly = typeof(HolidayCalendarService).Assembly;
        var resourceName = assembly.GetManifestResourceNames()
            .FirstOrDefault(name => name.EndsWith(fileName, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"Bundled holiday resource '{fileName}' was not found.");

        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Bundled holiday resource '{fileName}' could not be opened.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}

internal static class JsonElementExtensions
{
    public static bool TryGetPropertyCaseInsensitive(this JsonElement element, string name, out JsonElement value)
    {
        if (element.TryGetProperty(name, out value))
        {
            return true;
        }

        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }
}