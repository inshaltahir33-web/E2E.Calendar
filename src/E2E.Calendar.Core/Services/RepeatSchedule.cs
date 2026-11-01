namespace E2E.Calendar.Core.Services;

/// <summary>One option of the "Repeat until done" picker. A null interval means the alert fires once.</summary>
public sealed record RepeatChoice(string Label, int? Minutes);

/// <summary>
/// The repeat options shared by the event editor's alert rows and the Reminders add row.
/// The interval becomes <c>Alert.RepeatEveryMinutes</c>, which is what makes the engine
/// queue another nagging instance after each fire until the user chooses Done or Snooze.
/// </summary>
public static class RepeatSchedule
{
    public static IReadOnlyList<RepeatChoice> Choices { get; } =
    [
        new("Once", null),
        new("Every 5 min", 5),
        new("Every 10 min", 10),
        new("Every 15 min", 15),
        new("Every 30 min", 30),
    ];

    /// <summary>Maps a stored interval back to its option; unknown or null intervals mean "Once".</summary>
    public static RepeatChoice ForMinutes(int? minutes) =>
        Choices.FirstOrDefault(choice => choice.Minutes == minutes) ?? Choices[0];

    /// <summary>Maps a picker index to the interval it stores; an empty picker means "Once".</summary>
    public static int? MinutesAt(int index) =>
        index >= 0 && index < Choices.Count ? Choices[index].Minutes : null;
}
