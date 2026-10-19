using E2E.Calendar.Core.Models;

namespace E2E.Calendar.Data.Types;

public sealed class TaskRow
{
    public string Id { get; init; } = string.Empty;

    public string? ListId { get; init; }

    public string? Title { get; init; }

    public string? Notes { get; init; }

    public string? DueUtc { get; init; }

    public long HasTime { get; init; }

    public int Priority { get; init; }

    public string? RRule { get; init; }

    public string? DoneUtc { get; init; }

    public string? ParentId { get; init; }

    public string? LinkedEventId { get; init; }

    public long Dirty { get; init; }

    public long Deleted { get; init; }

    public static TaskRow FromModel(TaskItem model) => new()
    {
        Id = model.Id,
        ListId = model.ListId,
        Title = model.Title,
        Notes = model.Notes,
        DueUtc = model.DueUtc is null ? null : SqliteTime.ToUtcText(model.DueUtc.Value),
        HasTime = model.HasTime ? 1 : 0,
        Priority = (int)model.Priority,
        RRule = model.RRule,
        DoneUtc = model.DoneUtc is null ? null : SqliteTime.ToUtcText(model.DoneUtc.Value),
        ParentId = model.ParentId,
        LinkedEventId = model.LinkedEventId,
        Dirty = model.Dirty ? 1 : 0,
        Deleted = model.Deleted ? 1 : 0,
    };
}
