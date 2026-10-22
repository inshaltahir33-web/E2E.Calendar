using E2E.Calendar.Core.Abstractions;
using E2E.Calendar.Core.Models;
using E2E.Calendar.Core.Sync;

namespace E2E.Calendar.Core.Services;

public sealed class EventService : IEventService
{
    public static readonly TimeSpan DefaultUndoWindow = TimeSpan.FromSeconds(10);

    private readonly IEventRepository _events;
    private readonly IOutbox _outbox;
    private readonly IClock _clock;
    private readonly IEventQueryCache? _queryCache;
    private readonly IAlertRepository? _alertRepository;

    public EventService(
        IEventRepository events,
        IOutbox outbox,
        IClock clock,
        TimeSpan? undoWindow = null,
        IEventQueryCache? queryCache = null,
        IAlertRepository? alertRepository = null)
    {
        _events = events;
        _outbox = outbox;
        _clock = clock;
        _queryCache = queryCache;
        _alertRepository = alertRepository;
        UndoWindow = undoWindow ?? DefaultUndoWindow;
    }

    public TimeSpan UndoWindow { get; }

    public async Task<CalendarEvent> CreateAsync(EventDraft draft, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(draft);

        EnsureNotEmpty(draft.Title, nameof(draft));
        EnsureNotEmpty(draft.CalendarId, nameof(draft));

        var startUtc = draft.StartUtc.ToUniversalTime();
        var endUtc = draft.EndUtc.ToUniversalTime();

        EnsureValidRange(startUtc, endUtc, nameof(draft));

        var now = _clock.UtcNow;

        var calendarEvent = new CalendarEvent
        {
            Id = NewId(),
            CalendarId = draft.CalendarId,
            Title = draft.Title,
            Notes = draft.Notes,
            Location = draft.Location,
            Url = draft.Url,
            StartUtc = startUtc,
            EndUtc = endUtc,
            AllDay = draft.AllDay,
            TimeZoneId = draft.TimeZoneId,
            RRule = draft.RRule,
            ExDates = draft.ExDates,
            Color = draft.Color,
            MeetingUrl = draft.MeetingUrl,
            TravelMinutes = draft.TravelMinutes,
            CreatedUtc = now,
            UpdatedUtc = now,
            Deleted = false,
            Dirty = true,
        };

        await _events.UpsertAsync(calendarEvent, cancellationToken);
        await EnqueueAsync(calendarEvent, OutboxOp.Upsert, cancellationToken);

        return calendarEvent;
    }

    public async Task AddAlertsAsync(
        string eventId,
        IReadOnlyList<int> offsetMinutes,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventId);
        ArgumentNullException.ThrowIfNull(offsetMinutes);

        if (_alertRepository is null || offsetMinutes.Count == 0)
        {
            return;
        }

        foreach (var minutes in offsetMinutes.Distinct())
        {
            await _alertRepository.UpsertAlertAsync(
                new Alert
                {
                    Id = NewId(),
                    OwnerType = OwnerType.Event,
                    OwnerId = eventId,
                    TriggerOffsetMinutes = minutes,
                    Priority = Priority.Normal,
                },
                cancellationToken);
        }
    }

    public async Task<CalendarEvent> UpdateAsync(CalendarEvent calendarEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(calendarEvent);

        EnsureNotEmpty(calendarEvent.Title, nameof(calendarEvent));

        var startUtc = calendarEvent.StartUtc.ToUniversalTime();
        var endUtc = calendarEvent.EndUtc.ToUniversalTime();

        EnsureValidRange(startUtc, endUtc, nameof(calendarEvent));

        var stored = await _events.GetByIdAsync(calendarEvent.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"Event '{calendarEvent.Id}' does not exist.");

        var updated = stored with
        {
            CalendarId = calendarEvent.CalendarId,
            Title = calendarEvent.Title,
            Notes = calendarEvent.Notes,
            Location = calendarEvent.Location,
            Url = calendarEvent.Url,
            StartUtc = startUtc,
            EndUtc = endUtc,
            AllDay = calendarEvent.AllDay,
            TimeZoneId = calendarEvent.TimeZoneId,
            RRule = calendarEvent.RRule,
            ExDates = calendarEvent.ExDates,
            Color = calendarEvent.Color,
            MeetingUrl = calendarEvent.MeetingUrl,
            TravelMinutes = calendarEvent.TravelMinutes,
            UpdatedUtc = _clock.UtcNow,
            Dirty = true,
        };

        await _events.UpsertAsync(updated, cancellationToken);
        await EnqueueAsync(updated, OutboxOp.Upsert, cancellationToken);

        return updated;
    }

    public async Task<CalendarEvent> SoftDeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        var stored = await GetRequiredAsync(id, cancellationToken);

        var deleted = stored with
        {
            Deleted = true,
            UpdatedUtc = _clock.UtcNow,
            Dirty = true,
        };

        await _events.UpsertAsync(deleted, cancellationToken);
        await EnqueueAsync(deleted, OutboxOp.Delete, cancellationToken);

        return deleted;
    }

    public async Task<CalendarEvent> UndoDeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        var stored = await GetRequiredAsync(id, cancellationToken);

        if (!stored.Deleted)
        {
            throw new InvalidOperationException($"Event '{id}' is not deleted.");
        }

        var now = _clock.UtcNow;

        if (now - stored.UpdatedUtc > UndoWindow)
        {
            throw new InvalidOperationException($"The undo window for event '{id}' has expired.");
        }

        var restored = stored with
        {
            Deleted = false,
            UpdatedUtc = now,
            Dirty = true,
        };

        await _events.UpsertAsync(restored, cancellationToken);
        await EnqueueAsync(restored, OutboxOp.Upsert, cancellationToken);

        return restored;
    }

    public async Task<CalendarEvent> DuplicateAsync(string id, CancellationToken cancellationToken = default)
    {
        var source = await GetRequiredAsync(id, cancellationToken);

        var now = _clock.UtcNow;

        var duplicate = source with
        {
            Id = NewId(),
            RemoteId = null,
            ETag = null,
            RecurrenceMasterId = null,
            RecurrenceIdUtc = null,
            CreatedUtc = now,
            UpdatedUtc = now,
            Deleted = false,
            Dirty = true,
        };

        await _events.UpsertAsync(duplicate, cancellationToken);
        await EnqueueAsync(duplicate, OutboxOp.Upsert, cancellationToken);

        return duplicate;
    }

    private async Task<CalendarEvent> GetRequiredAsync(string id, CancellationToken cancellationToken)
    {
        return await _events.GetByIdAsync(id, cancellationToken)
            ?? throw new KeyNotFoundException($"Event '{id}' does not exist.");
    }

    private async Task EnqueueAsync(CalendarEvent calendarEvent, OutboxOp op, CancellationToken cancellationToken)
    {
        await _outbox.EnqueueAsync(
            new OutboxItem
            {
                Entity = OutboxEntities.Event,
                EntityId = calendarEvent.Id,
                Op = op,
            },
            cancellationToken);

        _queryCache?.Invalidate();
    }

    private static string NewId() => Guid.NewGuid().ToString("N");

    private static void EnsureNotEmpty(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value cannot be empty.", parameterName);
        }
    }

    private static void EnsureValidRange(DateTimeOffset startUtc, DateTimeOffset endUtc, string parameterName)
    {
        if (endUtc < startUtc)
        {
            throw new ArgumentException("The event cannot end before it starts.", parameterName);
        }
    }
}
