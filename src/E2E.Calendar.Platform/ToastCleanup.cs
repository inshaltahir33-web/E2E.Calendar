using Windows.UI.Notifications;

namespace E2E.Calendar.Platform;

/// <summary>
/// Best-effort removal of this app's toasts (displayed and OS-scheduled). Shared by
/// the reminder scheduler (re-plan / done / deleted cleanup) and the toast
/// activation service (Done button).
/// </summary>
internal static class ToastCleanup
{
    public const string Group = "reminders";

    /// <summary>Removes every toast scheduled for the reminder group.</summary>
    public static void RemoveScheduledGroup(ToastNotifier notifier)
    {
        foreach (var scheduled in notifier.GetScheduledToastNotifications().ToList())
        {
            if (string.Equals(scheduled.Group, Group, StringComparison.Ordinal))
            {
                try
                {
                    notifier.RemoveFromSchedule(scheduled);
                }
                catch (Exception)
                {
                    // The toast may already have fired or been removed.
                }
            }
        }
    }

    /// <summary>Removes a displayed and/or scheduled toast by its tag.</summary>
    public static void RemoveByTag(string tag)
    {
        try
        {
            var notifier = ToastNotificationManager.CreateToastNotifier();

            foreach (var scheduled in notifier.GetScheduledToastNotifications().ToList())
            {
                if (string.Equals(scheduled.Tag, tag, StringComparison.Ordinal))
                {
                    try
                    {
                        notifier.RemoveFromSchedule(scheduled);
                    }
                    catch (Exception)
                    {
                        // The toast may already have fired or been removed.
                    }
                }
            }

            ToastNotificationManager.History.Remove(tag, Group);
        }
        catch (Exception)
        {
            // Removal is best-effort; the next re-plan pass clears leftovers.
        }
    }
}
