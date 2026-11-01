using System.Reflection;
using E2E.Calendar.Core.Abstractions;
using E2E.Calendar.Core.Models;
using E2E.Calendar.Core.Services;
using E2E.Calendar.Sync.Ics;
using CalendarModel = E2E.Calendar.Core.Models.Calendar;

namespace E2E.Calendar.App.Services;

/// <summary>
/// Installs read-only, bundled holiday calendars (one per country) from embedded
/// ICS resources so the app works fully offline.
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

    private readonly ICalendarRepository _calendars;
    private readonly CalendarService _calendarCatalog;
    private readonly IEventRepository _events;
    private readonly IcsService _ics;
    private readonly IClock _clock;
    private readonly IEventQueryCache _cache;

    public HolidayCalendarService(
        ICalendarRepository calendars,
        CalendarService calendarCatalog,
        IEventRepository events,
        IcsService ics,
        IClock clock,
        IEventQueryCache cache)
    {
        _calendars = calendars;
        _calendarCatalog = calendarCatalog;
        _events = events;
        _ics = ics;
        _clock = clock;
        _cache = cache;
    }

    public static string CalendarId(string countryCode) => $"holiday-{countryCode}";

    public async Task<bool> IsEnabledAsync(string countryCode, CancellationToken cancellationToken = default) =>
        await _calendars.GetByIdAsync(CalendarId(countryCode), cancellationToken) is not null;

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
        var events = _ics.Import(ics, calendar.Id, now)
            .Select(item => item with
            {
                Id = $"{calendar.Id}:{item.Id}",
                CalendarId = calendar.Id,
                Color = CalendarPalette.Holiday,
                Dirty = false,
            })
            .ToList();

        await _events.DeleteByCalendarIdsAsync([calendar.Id], cancellationToken);

        foreach (var calendarEvent in events)
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