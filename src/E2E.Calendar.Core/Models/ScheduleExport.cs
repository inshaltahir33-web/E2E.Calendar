namespace E2E.Calendar.Core.Models;

public enum ScheduleExportView
{
    Day,
    Week,
    Month,
}

public sealed record ScheduleExportDocument
{
    public required ScheduleExportView View { get; init; }

    public required DateTimeOffset FromUtc { get; init; }

    public required DateTimeOffset ToUtc { get; init; }

    public required IReadOnlyList<CalendarEvent> Events { get; init; }
}
