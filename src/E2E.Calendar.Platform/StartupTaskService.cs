using Windows.ApplicationModel;

namespace E2E.Calendar.Platform;

/// <summary>
/// Bridges the packaged <see cref="StartupTask"/> (declared in Package.appxmanifest)
/// to the settings UI. Unpackaged builds have no package identity, so every call
/// degrades to "not supported" instead of throwing.
/// </summary>
public sealed class StartupTaskService
{
    public const string TaskId = "E2ECalendarStartup";

    public async Task<bool> IsSupportedAsync()
    {
        try
        {
            await StartupTask.GetAsync(TaskId);

            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public async Task<bool> IsEnabledAsync()
    {
        try
        {
            var task = await StartupTask.GetAsync(TaskId);

            return IsEnabled(task.State);
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>Enables or disables the task and returns the state the OS ended up in.</summary>
    public async Task<bool> SetEnabledAsync(bool enabled)
    {
        try
        {
            var task = await StartupTask.GetAsync(TaskId);

            if (enabled)
            {
                var state = await task.RequestEnableAsync();

                return IsEnabled(state);
            }

            task.Disable();

            return false;
        }
        catch (Exception)
        {
            // StartupTask.RequestEnableAsync can also throw when the user disabled the
            // task from Task Manager; report the unchanged (disabled) state.
            return false;
        }
    }

    private static bool IsEnabled(StartupTaskState state) =>
        state is StartupTaskState.Enabled or StartupTaskState.EnabledByPolicy;
}
