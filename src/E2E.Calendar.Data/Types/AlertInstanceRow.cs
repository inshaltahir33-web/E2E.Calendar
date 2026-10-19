using E2E.Calendar.Core.Models;

namespace E2E.Calendar.Data.Types;

public sealed class AlertInstanceRow
{
    public string Id { get; init; } = string.Empty;

    public string? AlertId { get; init; }

    public string OwnerType { get; init; } = string.Empty;

    public string? OwnerId { get; init; }

    public string? OccurrenceUtc { get; init; }

    public string? FireAtUtc { get; init; }

    public string State { get; init; } = string.Empty;

    public string? SnoozedUntilUtc { get; init; }

    public string? FiredUtc { get; init; }

    public long RepeatCount { get; init; }

    public static AlertInstanceRow FromModel(AlertInstance model) => new()
    {
        Id = model.Id,
        AlertId = model.AlertId,
        OwnerType = EnumText.ToText(model.OwnerType),
        OwnerId = model.OwnerId,
        OccurrenceUtc = SqliteTime.ToUtcText(model.OccurrenceUtc),
        FireAtUtc = SqliteTime.ToUtcText(model.FireAtUtc),
        State = EnumText.ToText(model.State),
        SnoozedUntilUtc = model.SnoozedUntilUtc is null ? null : SqliteTime.ToUtcText(model.SnoozedUntilUtc.Value),
        FiredUtc = model.FiredUtc is null ? null : SqliteTime.ToUtcText(model.FiredUtc.Value),
        RepeatCount = model.RepeatCount,
    };
}
