using System.Security.Cryptography;
using System.Text;
using Microsoft.Windows.AppNotifications;

namespace E2E.Calendar.Platform;

/// <summary>
/// Best-effort removal of this app's displayed toasts. Shared by the reminder
/// scheduler (done / deleted cleanup) and the toast activation service.
/// </summary>
internal static class ToastCleanup
{
    public const string Group = "reminders";

    /// <summary>Tag of the wake-from-sleep catch-up toast; reused so a later resume replaces it.</summary>
    public const string SummaryTag = "missed-summary";

    /// <summary>
    /// Windows caps a toast tag at 16 characters. Hashing keeps the tag unique per
    /// instance; truncating the raw id made instances with a shared prefix collide.
    /// </summary>
    public static string TagFor(string instanceId) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(instanceId)).AsSpan(0, 8));

    /// <summary>Removes a displayed toast by its instance id.</summary>
    public static async Task RemoveByTagAsync(string instanceId)
    {
        try
        {
            await AppNotificationManager.Default.RemoveByTagAndGroupAsync(TagFor(instanceId), Group);
        }
        catch (Exception)
        {
            // Removal is best-effort; the toast disappears on its own.
        }
    }
}
