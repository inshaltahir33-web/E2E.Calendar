using System.Text.Json;
using E2E.Calendar.Core.Abstractions;
using E2E.Calendar.Core.Models;
using E2E.Calendar.Core.Recurrence;
using E2E.Calendar.Core.Sync;

namespace E2E.Calendar.Core.Services;

/// <summary>
/// The rows as they were before a delete, so Undo can put the series back exactly
/// (restoring an EXDATE list or a truncated rule, not just clearing a deleted flag).
/// </summary>
public sealed record EventDeletion(IReadOnlyList<CalendarEvent> Restore, DateTimeOffset DeletedUtc);

public sealed class EventDeletionService
{
    private readonly IEventRepository _events;
    private readonly IOutbox _outbox;
    private readonly IClock _clock;
    private readonly IEventQueryCache? _queryCache;

    public EventDeletionService(
        IEventRepository events,
        IOutbox outbox,
        IClock clock,
        IEventQueryCache? queryCache = null)
    {
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(outbox);
        ArgumentNullException.ThrowIfNull(clock);

        _events = events;
        _outbox = outbox;
        _clock = clock;
        _queryCache = queryCache;
    }

    public TimeSpan UndoWindow => EventService.DefaultUndoWindow;

    public async Task<EventDeletion> DeleteAsync(
        CalendarEvent target,
        DateTimeOffset occurrenceStartUtc,
        RecurrenceEditScope scope,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(target);

        var now = _clock.UtcNow;
        var restore = new List<CalendarEvent>();
        var master = target.RecurrenceMasterId is { } masterId
            ? await _events.GetByIdAsync(masterId, cancellationToken)
            : null;

        // An edited single occurrence ("exception") has no rule of its own. "This event"
        // retires that row and excludes its original slot, otherwise the master's plain
        // occurrence would take its place; the other scopes narrow or delete the series.
        if (target.RecurrenceMasterId is not null)
        {
            var originalStart = target.RecurrenceIdUtc ?? occurrenceStartUtc;

            if (master is null)
            {
                restore.Add(target);
                await SaveAsync(SoftDeleted(target, now), OutboxOp.Delete, cancellationToken);
            }
            else if (scope == RecurrenceEditScope.ThisEvent)
            {
                restore.Add(target);
                restore.Add(master);
                await SaveAsync(SoftDeleted(target, now), OutboxOp.Delete, cancellationToken);
                await SaveMasterAsync(master, originalStart, scope, now, cancellationToken);
            }
            else
            {
                restore.Add(master);
                await SaveMasterAsync(master, originalStart, scope, now, cancellationToken);
            }
        }
        else if (string.IsNullOrWhiteSpace(target.RRule))
        {
            restore.Add(target);
            await SaveAsync(SoftDeleted(target, now), OutboxOp.Delete, cancellationToken);
        }
        else
        {
            restore.Add(target);
            await SaveMasterAsync(target, occurrenceStartUtc, scope, now, cancellationToken);
        }

        _queryCache?.Invalidate();

        return new EventDeletion(restore, now);
    }

    public async Task UndoAsync(EventDeletion deletion, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(deletion);

        var now = _clock.UtcNow;
        if (now - deletion.DeletedUtc > UndoWindow)
        {
            throw new InvalidOperationException("The undo window for this deletion has expired.");
        }

        foreach (var original in deletion.Restore)
        {
            await SaveAsync(
                original with { Deleted = false, UpdatedUtc = now, Dirty = true },
                OutboxOp.Upsert,
                cancellationToken);
        }

        _queryCache?.Invalidate();
    }

    private async Task SaveMasterAsync(
        CalendarEvent master,
        DateTimeOffset occurrenceStartUtc,
        RecurrenceEditScope scope,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (scope == RecurrenceEditScope.ThisEvent)
        {
            // Keep the rule and every other occurrence; hide only this one.
            var exDates = ExDateList.Parse(master.ExDates).ToList();
            var occurrenceUtc = occurrenceStartUtc.ToUniversalTime();

            if (!exDates.Contains(occurrenceUtc))
            {
                exDates.Add(occurrenceUtc);
            }

            await SaveAsync(
                master with
                {
                    ExDates = ExDateList.Format(exDates),
                    UpdatedUtc = now,
                    Dirty = true,
                },
                OutboxOp.Upsert,
                cancellationToken);

            return;
        }

        if (scope == RecurrenceEditScope.ThisAndFollowing)
        {
            var previous = SeriesSplitter
                .SplitSeries(master, occurrenceStartUtc, master.RRule ?? string.Empty, nowUtc: now)
                .Previous with
            {
                UpdatedUtc = now,
                Dirty = true,
            };

            await SaveAsync(previous, OutboxOp.Upsert, cancellationToken);
            return;
        }

        await SaveAsync(SoftDeleted(master, now), OutboxOp.Delete, cancellationToken);
    }

    private static CalendarEvent SoftDeleted(CalendarEvent calendarEvent, DateTimeOffset now) =>
        calendarEvent with
        {
            Deleted = true,
            UpdatedUtc = now,
            Dirty = true,
        };

    private async Task SaveAsync(CalendarEvent calendarEvent, OutboxOp op, CancellationToken cancellationToken)
    {
        await _events.UpsertAsync(calendarEvent, cancellationToken);

        await _outbox.EnqueueAsync(
            new OutboxItem
            {
                Entity = OutboxEntities.Event,
                EntityId = calendarEvent.Id,
                Op = op,
                Payload = JsonSerializer.Serialize(calendarEvent),
            },
            cancellationToken);
    }
}
