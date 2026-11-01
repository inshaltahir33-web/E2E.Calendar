using E2E.Calendar.Core.Models;

namespace E2E.Calendar.Core.Services;

/// <summary>
/// The single toast shown after the PC wakes with reminders that came due while it slept.
/// Instances are deduplicated by owner so several alerts (or queued repeats) of the same
/// reminder still count as one missed reminder.
/// </summary>
public static class MissedReminderSummary
{
    public static int Count(IEnumerable<AlertInstance> missed)
    {
        ArgumentNullException.ThrowIfNull(missed);

        return missed
            .Select(instance => (instance.OwnerType, instance.OwnerId))
            .Distinct()
            .Count();
    }

    public static string Title(int count) =>
        count == 1 ? "1 missed reminder" : $"{count} missed reminders";
}
