using E2E.Calendar.Core.Models;

namespace E2E.Calendar.Data.Types;

public sealed class AlertRow
{
    public string Id { get; init; } = string.Empty;

    public string OwnerType { get; init; } = string.Empty;

    public string? OwnerId { get; init; }

    public long? TriggerOffsetMinutes { get; init; }

    public string? TriggerAtUtc { get; init; }

    public int Priority { get; init; }

    public long Persistent { get; init; }

    public long? RepeatEveryMinutes { get; init; }

    public string? Sound { get; init; }

    public static AlertRow FromModel(Alert model) => new()
    {
        Id = model.Id,
        OwnerType = EnumText.ToText(model.OwnerType),
        OwnerId = model.OwnerId,
        TriggerOffsetMinutes = model.TriggerOffsetMinutes,
        TriggerAtUtc = model.TriggerAtUtc is null ? null : SqliteTime.ToUtcText(model.TriggerAtUtc.Value),
        Priority = (int)model.Priority,
        Persistent = model.Persistent ? 1 : 0,
        RepeatEveryMinutes = model.RepeatEveryMinutes,
        Sound = model.Sound,
    };
}
