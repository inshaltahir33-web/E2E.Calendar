namespace E2E.Calendar.Core.Recurrence;

public enum RecurrenceFrequency
{
    None = 0,
    Daily = 1,
    Weekly = 2,
    Monthly = 3,
    Yearly = 4,
}

public enum RecurrenceEnd
{
    Never = 0,
    OnDate = 1,
    AfterCount = 2,
}

public enum MonthlyPattern
{
    ByDate = 0,
    ByWeekday = 1,
}

public sealed record RecurrenceRuleOptions
{
    public RecurrenceFrequency Frequency { get; init; } = RecurrenceFrequency.None;

    public int Interval { get; init; } = 1;

    public IReadOnlyList<DayOfWeek> WeeklyDays { get; init; } = Array.Empty<DayOfWeek>();

    public MonthlyPattern MonthlyPattern { get; init; } = MonthlyPattern.ByDate;

    public int MonthlyDay { get; init; } = 1;

    public int MonthlyWeekdayOrdinal { get; init; } = 1;

    public DayOfWeek MonthlyWeekday { get; init; } = DayOfWeek.Monday;

    public int YearlyMonth { get; init; } = 1;

    public RecurrenceEnd End { get; init; } = RecurrenceEnd.Never;

    public DateOnly? EndDate { get; init; }

    public int? Count { get; init; }
}
