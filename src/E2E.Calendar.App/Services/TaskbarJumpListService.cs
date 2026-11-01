using Microsoft.Extensions.Logging;
using Windows.UI.StartScreen;

namespace E2E.Calendar.App.Services;

/// <summary>
/// Populates the taskbar jump list with quick actions.
/// </summary>
public sealed class TaskbarJumpListService
{
    private readonly ILogger<TaskbarJumpListService> _logger;

    public TaskbarJumpListService(ILogger<TaskbarJumpListService> logger)
    {
        _logger = logger;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var jumpList = await JumpList.LoadCurrentAsync();
            jumpList.SystemGroupKind = JumpListSystemGroupKind.None;

            jumpList.Items.Clear();

            jumpList.Items.Add(CreateItem("new-event", "New event", "e2e-calendar://new-event"));
            jumpList.Items.Add(CreateItem("new-reminder", "New reminder", "e2e-calendar://new-reminder"));
            jumpList.Items.Add(CreateItem("today", "Today", "e2e-calendar://today"));

            await jumpList.SaveAsync();
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Taskbar jump list could not be initialized.");
        }
    }

    private static JumpListItem CreateItem(string id, string title, string arguments)
    {
        var item = JumpListItem.CreateWithArguments(arguments, title);
        return item;
    }
}
