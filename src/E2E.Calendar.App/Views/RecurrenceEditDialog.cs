using E2E.Calendar.App.Services;
using E2E.Calendar.Core.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;

namespace E2E.Calendar.App.Views;

public static class RecurrenceEditDialog
{
    public static Task<RecurrenceEditScope?> ShowAsync(IDialogService dialogs, XamlRoot? xamlRoot) =>
        ShowCoreAsync(dialogs, xamlRoot, "Edit a repeating event", "Save", "All events");

    public static Task<RecurrenceEditScope?> ShowDeleteAsync(IDialogService dialogs, XamlRoot? xamlRoot) =>
        ShowCoreAsync(dialogs, xamlRoot, "Delete a repeating event", "Delete", "All");

    private static async Task<RecurrenceEditScope?> ShowCoreAsync(
        IDialogService dialogs,
        XamlRoot? xamlRoot,
        string title,
        string primaryButtonText,
        string allLabel)
    {
        ArgumentNullException.ThrowIfNull(dialogs);

        var thisEvent = new RadioButton { Content = "This event", IsChecked = true };
        var thisAndFollowing = new RadioButton { Content = "This and following" };
        var allEvents = new RadioButton { Content = allLabel };

        var panel = new StackPanel { Spacing = 8 };
        panel.Children.Add(thisEvent);
        panel.Children.Add(thisAndFollowing);
        panel.Children.Add(allEvents);

        var dialog = new ContentDialog
        {
            XamlRoot = xamlRoot,
            Title = title,
            Content = panel,
            PrimaryButtonText = primaryButtonText,
            CloseButtonText = "Cancel",
        };

        AutomationProperties.SetName(dialog, title);

        var result = await dialogs.ShowAsync(dialog);

        if (result != ContentDialogResult.Primary)
        {
            return null;
        }

        if (thisEvent.IsChecked == true)
        {
            return RecurrenceEditScope.ThisEvent;
        }

        return thisAndFollowing.IsChecked == true
            ? RecurrenceEditScope.ThisAndFollowing
            : RecurrenceEditScope.AllEvents;
    }
}
