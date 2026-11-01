using E2E.Calendar.Core.Abstractions;

namespace E2E.Calendar.Core.Services;

public sealed record DailyDigest(int EventCount, int ReminderCount)
{
    public string Title =>
        $"Today: {EventCount} {EventLabel(EventCount)}, {ReminderCount} {ReminderLabel(ReminderCount)}";

    private static string EventLabel(int count) => count == 1 ? "event" : "events";

    private static string ReminderLabel(int count) => count == 1 ? "reminder" : "reminders";
}

/// <summary>
/// Decides when the morning digest is due and counts the local day's events and
/// still-open reminders for its summary text.
/// </summary>
public sealed class DailyDigestService
{
    /// <summary>
    /// How long after the chosen time a digest may still be sent, so a launch after
    /// the scheduled minute (or a restart) still produces the morning summary.
    /// </summary>
    public static readonly TimeSpan CatchUpWindow = TimeSpan.FromHours(4);

    private readonly EventQueryService _query;
    private readonly IReminderRepository _reminders;
    private readonly IClock _clock;

    public DailyDigestService(EventQueryService query, IReminderRepository reminders, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(reminders);
        ArgumentNullException.ThrowIfNull(clock);

        _query = query;
        _reminders = reminders;
        _clock = clock;
    }

    /// <summary>
    /// True once the local clock passes today's digest time, until the catch-up
    /// window closes, and only when today's digest has not been sent yet.
    /// </summary>
    public bool IsDue(DateTimeOffset nowUtc, TimeSpan timeOfDay, DateOnly? lastSentLocalDate)
    {
        var localNow = TimeZoneInfo.ConvertTime(nowUtc, _clock.LocalTimeZone);
        var today = DateOnly.FromDateTime(localNow.DateTime);

        if (lastSentLocalDate == today)
        {
            return false;
        }

        var scheduled = today.ToDateTime(TimeOnly.MinValue) + timeOfDay;

        return localNow.DateTime >= scheduled && localNow.DateTime < scheduled + CatchUpWindow;
    }

    public async Task<DailyDigest> BuildAsync(
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default)
    {
        var zone = _clock.LocalTimeZone;
        var localNow = TimeZoneInfo.ConvertTime(nowUtc, zone);
        var today = DateOnly.FromDateTime(localNow.DateTime);
        var startUtc = LocalToUtc(today.ToDateTime(TimeOnly.MinValue), zone);
        var endUtc = LocalToUtc(today.AddDays(1).ToDateTime(TimeOnly.MinValue), zone);

        var events = await _query.GetItemsAsync(startUtc, endUtc, cancellationToken: cancellationToken);
        var reminders = await _reminders.GetInRangeAsync(startUtc, endUtc, cancellationToken);

        var reminderCount = reminders.Count(reminder =>
            !reminder.Deleted &&
            reminder.DoneUtc is null &&
            reminder.DueUtc is { } due &&
            due >= startUtc &&
            due < endUtc);

        return new DailyDigest(events.Count, reminderCount);
    }

    private static DateTimeOffset LocalToUtc(DateTime local, TimeZoneInfo zone)
    {
        var value = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);

        // Local midnight can be skipped (or repeated) on a DST transition; step past
        // the gap instead of throwing so the day still has a usable UTC range.
        for (var guard = 0; guard < 8 && zone.IsInvalidTime(value); guard++)
        {
            value = value.AddMinutes(30);
        }

        return new DateTimeOffset(value, zone.GetUtcOffset(value));
    }
}
