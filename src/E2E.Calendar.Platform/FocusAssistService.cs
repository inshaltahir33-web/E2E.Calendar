using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace E2E.Calendar.Platform;

/// <summary>
/// Best-effort detection of Windows Focus Assist / Do Not Disturb through the
/// documented shell notification-state query. Full-screen and presentation states
/// are not treated as Do Not Disturb.
/// </summary>
public sealed class FocusAssistService
{
    private readonly ILogger<FocusAssistService> _logger;
    private bool? _isSupported;
    private bool _failureLogged;

    public FocusAssistService(ILogger<FocusAssistService> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    /// <summary>False when the OS does not answer the notification-state query.</summary>
    public bool IsSupported => _isSupported ??= QueryState() is not null;

    /// <summary>True while Focus Assist is set to quiet time (Do Not Disturb).</summary>
    public bool IsActive => IsSupported && QueryState() == QueryUserNotificationState.QuietTime;

    private QueryUserNotificationState? QueryState()
    {
        try
        {
            if (SHQueryUserNotificationState(out var state) == 0)
            {
                return state;
            }

            LogFailure(null);
            return null;
        }
        catch (Exception ex)
        {
            LogFailure(ex);
            return null;
        }
    }

    private void LogFailure(Exception? exception)
    {
        if (_failureLogged)
        {
            return;
        }

        _failureLogged = true;

        if (exception is null)
        {
            _logger.LogWarning("Focus Assist state could not be queried; critical break-through is unavailable.");
        }
        else
        {
            _logger.LogWarning(exception, "Focus Assist state could not be queried; critical break-through is unavailable.");
        }
    }

    private enum QueryUserNotificationState
    {
        NotPresent = 1,
        Busy = 2,
        RunningDirect3DFullScreen = 3,
        PresentationMode = 4,
        AcceptsNotifications = 5,
        QuietTime = 6,
        App = 7,
    }

    [DllImport("shell32.dll")]
    private static extern int SHQueryUserNotificationState(out QueryUserNotificationState state);
}
