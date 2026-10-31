namespace E2E.Calendar.Core.Services;

using System.Collections.ObjectModel;
using E2E.Calendar.Core.Abstractions;
using E2E.Calendar.Core.Models;

/// <summary>
/// Owns the one observable calendar list every calendar surface binds to, and
/// announces every change so those surfaces stay in sync without reloading on
/// their own.
/// </summary>
public sealed class CalendarService
{
    public const string PersonalCalendarId = "cal-personal";
    public const string WorkCalendarId = "cal-work";

    private readonly ICalendarRepository _calendars;
    private readonly ObservableCollection<Calendar> _items = new();
    private readonly ReadOnlyObservableCollection<Calendar> _readOnlyItems;
    private readonly SemaphoreSlim _loadGate = new(1, 1);

    private bool _loaded;

    public CalendarService(ICalendarRepository calendars)
    {
        ArgumentNullException.ThrowIfNull(calendars);

        _calendars = calendars;
        _readOnlyItems = new ReadOnlyObservableCollection<Calendar>(_items);
    }

    /// <summary>The shared list. Bind to this instead of keeping a private copy.</summary>
    public ReadOnlyObservableCollection<Calendar> Calendars => _readOnlyItems;

    /// <summary>
    /// Raised after a calendar is created, renamed, recoloured, deleted or added
    /// to / removed from through a subscription.
    /// </summary>
    public event EventHandler<CalendarListChangedEventArgs>? Changed;

    public async Task<IReadOnlyList<Calendar>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await EnsureLoadedAsync(cancellationToken);

        return _items.ToList();
    }

    /// <summary>Re-reads every calendar from storage, for changes made outside this service.</summary>
    public async Task<IReadOnlyList<Calendar>> ReloadAsync(CancellationToken cancellationToken = default)
    {
        var stored = await _calendars.GetAllAsync(cancellationToken);

        ReplaceAll(stored);
        _loaded = true;
        Raise(CalendarListChangeKind.Reloaded, null);

        return _items.ToList();
    }

    public async Task UpsertAsync(Calendar calendar, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(calendar);

        await EnsureLoadedAsync(cancellationToken);
        await _calendars.UpsertAsync(calendar, cancellationToken);

        var index = IndexOf(calendar.Id);

        if (index >= 0)
        {
            _items[index] = calendar;
        }
        else
        {
            _items.Add(calendar);
        }

        Raise(index >= 0 ? CalendarListChangeKind.Updated : CalendarListChangeKind.Created, calendar.Id);
    }

    public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        await EnsureLoadedAsync(cancellationToken);
        await _calendars.DeleteAsync(id, cancellationToken);

        var index = IndexOf(id);

        if (index >= 0)
        {
            _items.RemoveAt(index);
        }

        Raise(CalendarListChangeKind.Deleted, id);
    }

    public async Task<Calendar?> RenameAsync(
        string id,
        string name,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Calendar name is required.", nameof(name));
        }

        var calendar = await FindAsync(id, cancellationToken);

        if (calendar is null)
        {
            return null;
        }

        var renamed = calendar with { Name = name.Trim() };
        await UpsertAsync(renamed, cancellationToken);

        return renamed;
    }

    public async Task<Calendar?> SetColorAsync(
        string id,
        string? color,
        CancellationToken cancellationToken = default)
    {
        var calendar = await FindAsync(id, cancellationToken);

        if (calendar is null)
        {
            return null;
        }

        var recoloured = calendar with { Color = CalendarPalette.Normalize(color) };
        await UpsertAsync(recoloured, cancellationToken);

        return recoloured;
    }

    public async Task<Calendar?> SetVisibleAsync(
        string id,
        bool visible,
        CancellationToken cancellationToken = default)
    {
        var calendar = await FindAsync(id, cancellationToken);

        if (calendar is null || calendar.Visible == visible)
        {
            return calendar;
        }

        var updated = calendar with { Visible = visible };
        await UpsertAsync(updated, cancellationToken);

        return updated;
    }

    public async Task<Calendar> CreateLocalAsync(
        string name,
        string color,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Calendar name is required.", nameof(name));
        }

        var calendar = new Calendar
        {
            Id = $"cal-{Guid.NewGuid():N}",
            Name = name.Trim(),
            Color = CalendarPalette.Normalize(color),
            Visible = true,
            ReadOnly = false,
            IsLocal = true,
        };

        await UpsertAsync(calendar, cancellationToken);

        return calendar;
    }

    public async Task<IReadOnlyList<Calendar>> EnsureDefaultsAsync(CancellationToken cancellationToken = default)
    {
        var existing = await GetAllAsync(cancellationToken);

        if (existing.Count > 0)
        {
            return existing;
        }

        var personal = new Calendar
        {
            Id = PersonalCalendarId,
            Name = "Personal",
            Color = CalendarPalette.Personal,
            Visible = true,
            ReadOnly = false,
            IsLocal = true,
        };

        var work = new Calendar
        {
            Id = WorkCalendarId,
            Name = "Work",
            Color = CalendarPalette.Work,
            Visible = true,
            ReadOnly = false,
            IsLocal = true,
        };

        await UpsertAsync(personal, cancellationToken);
        await UpsertAsync(work, cancellationToken);

        return new[] { personal, work };
    }

    private async Task<Calendar?> FindAsync(string id, CancellationToken cancellationToken)
    {
        await EnsureLoadedAsync(cancellationToken);

        return _items.FirstOrDefault(calendar => string.Equals(calendar.Id, id, StringComparison.Ordinal));
    }

    private async Task EnsureLoadedAsync(CancellationToken cancellationToken)
    {
        if (_loaded)
        {
            return;
        }

        await _loadGate.WaitAsync(cancellationToken);

        try
        {
            if (_loaded)
            {
                return;
            }

            ReplaceAll(await _calendars.GetAllAsync(cancellationToken));
            _loaded = true;
        }
        finally
        {
            _loadGate.Release();
        }
    }

    private void ReplaceAll(IReadOnlyList<Calendar> calendars)
    {
        _items.Clear();

        foreach (var calendar in calendars)
        {
            _items.Add(calendar);
        }
    }

    private int IndexOf(string id)
    {
        for (var index = 0; index < _items.Count; index++)
        {
            if (string.Equals(_items[index].Id, id, StringComparison.Ordinal))
            {
                return index;
            }
        }

        return -1;
    }

    private void Raise(CalendarListChangeKind kind, string? calendarId) =>
        Changed?.Invoke(this, new CalendarListChangedEventArgs(kind, calendarId));
}

public enum CalendarListChangeKind
{
    Created,
    Updated,
    Deleted,
    Reloaded,
}

public sealed record CalendarListChangedEventArgs(CalendarListChangeKind Kind, string? CalendarId);
