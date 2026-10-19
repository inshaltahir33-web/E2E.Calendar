using E2E.Calendar.Core.Abstractions;
using E2E.Calendar.Core.Models;
using E2E.Calendar.Core.Recurrence;
using E2E.Calendar.Core.Sync;

namespace E2E.Calendar.Core.Services;

public enum RecurrenceEditScope
{
    ThisEvent = 0,
    ThisAndFollowing = 1,
    AllEvents = 2,
}

public sealed class RecurrenceEditService
{
    private readonly IEventRepository _events;
    private readonly IOutbox _outbox;
    private readonly IClock _clock;

    public RecurrenceEditService(IEventRepository events, IOutbox outbox, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(outbox);
        ArgumentNullException.ThrowIfNull(clock);

        _events = events;
        _outbox = outbox;
        _clock = clock;
    }

    public async Task<IReadOnlyList<CalendarEvent>> ApplyAsync(
        CalendarEvent master,
        DateTimeOffset occurrenceStartUtc,
        CalendarEvent edited,
        RecurrenceEditScope scope,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(master);
        ArgumentNullException.ThrowIfNull(edited);

        return scope switch
        {
            RecurrenceEditScope.ThisEvent => await ApplyToThisEventAsync(master, occurrenceStartUtc, edited, cancellationToken),
            RecurrenceEditScope.ThisAndFollowing => await ApplyToThisAndFollowingAsync(master, occurrenceStartUtc, edited, cancellationToken),
            _ => await ApplyToAllEventsAsync(master, edited, cancellationToken),
        };
    }

    private async Task<IReadOnlyList<CalendarEvent>> ApplyToThisEventAsync(
        CalendarEvent master,
        DateTimeOffset occurrenceStartUtc,
        CalendarEvent edited,
        CancellationToken cancellationToken)
    {
        var existing = (await _events.GetExceptionsAsync(
                master.Id,
                occurrenceStartUtc.AddDays(-1),
                occurrenceStartUtc.AddDays(1),
                cancellationToken))
            .FirstOrDefault(exception => exception.RecurrenceIdUtc == occurrenceStartUtc);

        var now = _clock.UtcNow;

        var exception = existing is null
            ? edited with
            {
                Id = Guid.NewGuid().ToString("N"),
                RecurrenceMasterId = master.Id,
                RecurrenceIdUtc = occurrenceStartUtc,
                CreatedUtc = now,
            }
            : existing with
            {
                Title = edited.Title,
                Notes = edited.Notes,
                Location = edited.Location,
                Url = edited.Url,
                MeetingUrl = edited.MeetingUrl,
                StartUtc = edited.StartUtc,
                EndUtc = edited.EndUtc,
                AllDay = edited.AllDay,
                TimeZoneId = edited.TimeZoneId,
                Color = edited.Color,
            };

        var saved = exception with
        {
            UpdatedUtc = now,
            Dirty = true,
            Deleted = false,
        };

        await SaveAsync(saved, cancellationToken);

        return new[] { saved };
    }

    private async Task<IReadOnlyList<CalendarEvent>> ApplyToThisAndFollowingAsync(
        CalendarEvent master,
        DateTimeOffset occurrenceStartUtc,
        CalendarEvent edited,
        CancellationToken cancellationToken)
    {
        var timeZone = master.TimeZoneId.TryToTimeZoneInfo(out var resolved) ? resolved : TimeZoneInfo.Utc;

        var occurrenceLocal = timeZone.ToLocal(occurrenceStartUtc);
        var editedLocal = timeZone.ToLocal(edited.StartUtc);
        var newStartLocal = occurrenceLocal.Date + editedLocal.TimeOfDay;
        var newStartUtc = timeZone.ToUtc(newStartLocal);
        var newDuration = edited.EndUtc - edited.StartUtc;

        var split = SeriesSplitter.SplitSeries(
            master,
            occurrenceStartUtc,
            edited.RRule ?? master.RRule ?? string.Empty,
            newStartUtc: newStartUtc,
            newTitle: edited.Title,
            nowUtc: _clock.UtcNow);

        var previous = split.Previous with
        {
            UpdatedUtc = _clock.UtcNow,
            Dirty = true,
        };

        var following = split.Following with
        {
            Notes = edited.Notes,
            Location = edited.Location,
            Url = edited.Url,
            MeetingUrl = edited.MeetingUrl,
            StartUtc = newStartUtc,
            EndUtc = newStartUtc + newDuration,
            AllDay = edited.AllDay,
            TimeZoneId = edited.TimeZoneId,
            Color = edited.Color,
            UpdatedUtc = _clock.UtcNow,
            Dirty = true,
        };

        await SaveAsync(previous, cancellationToken);
        await SaveAsync(following, cancellationToken);

        return new[] { previous, following };
    }

    private async Task<IReadOnlyList<CalendarEvent>> ApplyToAllEventsAsync(
        CalendarEvent master,
        CalendarEvent edited,
        CancellationToken cancellationToken)
    {
        var updated = master with
        {
            Title = edited.Title,
            Notes = edited.Notes,
            Location = edited.Location,
            Url = edited.Url,
            MeetingUrl = edited.MeetingUrl,
            StartUtc = edited.StartUtc,
            EndUtc = edited.EndUtc,
            AllDay = edited.AllDay,
            TimeZoneId = edited.TimeZoneId,
            RRule = edited.RRule,
            Color = edited.Color,
            UpdatedUtc = _clock.UtcNow,
            Dirty = true,
        };

        await SaveAsync(updated, cancellationToken);

        return new[] { updated };
    }

    private async Task SaveAsync(CalendarEvent calendarEvent, CancellationToken cancellationToken)
    {
        await _events.UpsertAsync(calendarEvent, cancellationToken);

        await _outbox.EnqueueAsync(
            new OutboxItem
            {
                Entity = OutboxEntities.Event,
                EntityId = calendarEvent.Id,
                Op = OutboxOp.Upsert,
            },
            cancellationToken);
    }
}
