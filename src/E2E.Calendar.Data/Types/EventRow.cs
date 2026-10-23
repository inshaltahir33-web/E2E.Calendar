using E2E.Calendar.Core.Models;

namespace E2E.Calendar.Data.Types;

public sealed class EventRow
{
    public string Id { get; init; } = string.Empty;

    public string CalendarId { get; init; } = string.Empty;

    public string? RemoteId { get; init; }

    public string? ETag { get; init; }

    public string? Title { get; init; }

    public string? Notes { get; init; }

    public string? Location { get; init; }

    public string? Url { get; init; }

    public string StartUtc { get; init; } = string.Empty;

    public string EndUtc { get; init; } = string.Empty;

    public long AllDay { get; init; }

    public string? TimeZoneId { get; init; }

    public string? RRule { get; init; }

    public string? ExDates { get; init; }

    public string? RecurrenceMasterId { get; init; }

    public string? RecurrenceIdUtc { get; init; }

    public string? Color { get; init; }

    public string? MeetingUrl { get; init; }

    public string? Attendees { get; init; }

    public long? TravelMinutes { get; init; }

    public string CreatedUtc { get; init; } = string.Empty;

    public string UpdatedUtc { get; init; } = string.Empty;

    public long Deleted { get; init; }

    public long Dirty { get; init; }

    public static EventRow FromModel(CalendarEvent model) => new()
    {
        Id = model.Id,
        CalendarId = model.CalendarId,
        RemoteId = model.RemoteId,
        ETag = model.ETag,
        Title = model.Title,
        Notes = model.Notes,
        Location = model.Location,
        Url = model.Url,
        StartUtc = SqliteTime.ToUtcText(model.StartUtc),
        EndUtc = SqliteTime.ToUtcText(model.EndUtc),
        AllDay = model.AllDay ? 1 : 0,
        TimeZoneId = model.TimeZoneId,
        RRule = model.RRule,
        ExDates = model.ExDates,
        RecurrenceMasterId = model.RecurrenceMasterId,
        RecurrenceIdUtc = model.RecurrenceIdUtc is null
            ? null
            : SqliteTime.ToUtcText(model.RecurrenceIdUtc.Value),
        Color = model.Color,
        MeetingUrl = model.MeetingUrl,
        Attendees = model.Attendees,
        TravelMinutes = model.TravelMinutes,
        CreatedUtc = SqliteTime.ToUtcText(model.CreatedUtc),
        UpdatedUtc = SqliteTime.ToUtcText(model.UpdatedUtc),
        Deleted = model.Deleted ? 1 : 0,
        Dirty = model.Dirty ? 1 : 0,
    };
}
