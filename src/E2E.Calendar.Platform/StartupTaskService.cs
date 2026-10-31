using Windows.ApplicationModel;

namespace E2E.Calendar.Platform;

public enum StartupStatus
{
    /// <summary>The app has no package identity or the task is not declared in its manifest.</summary>
    Unsupported,
    Disabled,
    DisabledByUser,
    DisabledByPolicy,
    Enabled,
    EnabledByPolicy,
}

/// <summary>
/// Bridges the packaged <see cref="StartupTask"/> (declared in Package.appxmanifest)
/// to the settings UI. Unpackaged builds have no package identity, so the status is
/// reported as <see cref="StartupStatus.Unsupported"/> instead of a dead toggle.
/// </summary>
public sealed class StartupTaskService
{
    public const string TaskId = "E2ECalendarStartup";

    public async Task<StartupStatus> GetStatusAsync()
    {
        if (!HasPackageIdentity())
        {
            return StartupStatus.Unsupported;
        }

        try
        {
            var task = await StartupTask.GetAsync(TaskId);

            return Map(task.State);
        }
        catch (Exception)
        {
            return StartupStatus.Unsupported;
        }
    }

    /// <summary>Enables or disables the task and returns the state the OS ended up in.</summary>
    public async Task<StartupStatus> SetEnabledAsync(bool enabled)
    {
        if (!HasPackageIdentity())
        {
            return StartupStatus.Unsupported;
        }

        try
        {
            var task = await StartupTask.GetAsync(TaskId);

            if (enabled)
            {
                return Map(await task.RequestEnableAsync());
            }

            task.Disable();

            return StartupStatus.Disabled;
        }
        catch (Exception)
        {
            // RequestEnableAsync can fail when the user disabled the task from Task Manager,
            // so report the state Windows actually reports rather than the requested one.
            return await GetStatusAsync();
        }
    }

    public static bool IsEnabled(StartupStatus status) =>
        status is StartupStatus.Enabled or StartupStatus.EnabledByPolicy;

    /// <summary>Whether the toggle can change the state without being refused by Windows.</summary>
    public static bool IsEditable(StartupStatus status) =>
        status is StartupStatus.Enabled or StartupStatus.Disabled;

    private static bool HasPackageIdentity()
    {
        try
        {
            _ = Package.Current.Id;

            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static StartupStatus Map(StartupTaskState state) =>
        state switch
        {
            StartupTaskState.Enabled => StartupStatus.Enabled,
            StartupTaskState.EnabledByPolicy => StartupStatus.EnabledByPolicy,
            StartupTaskState.DisabledByUser => StartupStatus.DisabledByUser,
            StartupTaskState.DisabledByPolicy => StartupStatus.DisabledByPolicy,
            _ => StartupStatus.Disabled,
        };
}
