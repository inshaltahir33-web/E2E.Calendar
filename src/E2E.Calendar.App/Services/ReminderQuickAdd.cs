using E2E.Calendar.Core;
using E2E.Calendar.Core.Abstractions;
using E2E.Calendar.Core.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;

namespace E2E.Calendar.App.Services;

/// <summary>
/// Creates a reminder from a short prompt. Used by the "New reminder here"
/// context menu actions, which need to work without a full reminder editor.
/// </summary>
public sealed class ReminderQuickAdd
{
    private readonly IReminderRepository _reminders;
    private readonly IClock _clock;
    private readonly IDialogService _dialogs;

    public ReminderQuickAdd(IReminderRepository reminders, IClock clock, IDialogService dialogs)
    {
        ArgumentNullException.ThrowIfNull(dialogs);

        _reminders = reminders;
        _clock = clock;
        _dialogs = dialogs;
    }

    public async Task<Reminder?> ShowAndCreateAsync(XamlRoot xamlRoot, DateTimeOffset dueUtc)
    {
        if (xamlRoot is null)
        {
            return null;
        }

        var input = new TextBox
        {
            PlaceholderText = "Reminder title",
            AcceptsReturn = false,
        };

        AutomationProperties.SetName(input, "Reminder title");

        var dialog = new ContentDialog
        {
            XamlRoot = xamlRoot,
            Title = "New reminder",
            Content = input,
            PrimaryButtonText = "Add",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
        };

        var result = await _dialogs.ShowAsync(dialog);

        if (result != ContentDialogResult.Primary)
        {
            return null;
        }

        var title = string.IsNullOrWhiteSpace(input.Text) ? "Reminder" : input.Text.Trim();
        var now = _clock.UtcNow;

        var reminder = new Reminder
        {
            Id = Guid.NewGuid().ToString("N"),
            Title = title,
            DueUtc = dueUtc,
            TimeZoneId = _clock.LocalTimeZone.ToIanaId(),
            CreatedUtc = now,
            UpdatedUtc = now,
        };

        await _reminders.UpsertAsync(reminder);

        return reminder;
    }
}
