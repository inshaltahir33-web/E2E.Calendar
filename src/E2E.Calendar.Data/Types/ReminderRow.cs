using E2E.Calendar.Core.Models;

namespace E2E.Calendar.Data.Types;

public sealed class ReminderRow
{
    public string Id { get; init; } = string.Empty;

    public string? Title { get; init; }

    public string? Notes { get; init; }

    public string? DueUtc { get; init; }

    public string? TimeZoneId { get; init; }

    public string? RRule { get; init; }

    public int Priority { get; init; }

    public long Flagged { get; init; }

    public string? DoneUtc { get; init; }

    public string? ListId { get; init; }

    public string? CreatedUtc { get; init; }

    public string? UpdatedUtc { get; init; }

    public long Deleted { get; init; }

    public long Dirty { get; init; }

    public static ReminderRow FromModel(Reminder model) => new()
    {
        Id = model.Id,
        Title = model.Title,
        Notes = model.Notes,
        DueUtc = model.DueUtc is null ? null : SqliteTime.ToUtcText(model.DueUtc.Value),
        TimeZoneId = model.TimeZoneId,
        RRule = model.RRule,
        Priority = (int)model.Priority,
        Flagged = model.Flagged ? 1 : 0,
        DoneUtc = model.DoneUtc is null ? null : SqliteTime.ToUtcText(model.DoneUtc.Value),
        ListId = model.ListId,
        CreatedUtc = SqliteTime.ToUtcText(model.CreatedUtc),
        UpdatedUtc = SqliteTime.ToUtcText(model.UpdatedUtc),
        Deleted = model.Deleted ? 1 : 0,
        Dirty = model.Dirty ? 1 : 0,
    };
}
