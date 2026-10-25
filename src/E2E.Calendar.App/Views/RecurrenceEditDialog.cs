using E2E.Calendar.App.Services;
using E2E.Calendar.Core.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace E2E.Calendar.App.Views;

public static class RecurrenceEditDialog
{
    public static async Task<RecurrenceEditScope?> ShowAsync(IDialogService dialogs, XamlRoot? xamlRoot)
    {
        ArgumentNullException.ThrowIfNull(dialogs);

        var thisEvent = new RadioButton { Content = "This event", IsChecked = true };
        var thisAndFollowing = new RadioButton { Content = "This and following" };
        var allEvents = new RadioButton { Content = "All events" };

        var panel = new StackPanel { Spacing = 8 };
        panel.Children.Add(thisEvent);
        panel.Children.Add(thisAndFollowing);
        panel.Children.Add(allEvents);

        var dialog = new ContentDialog
        {
            XamlRoot = xamlRoot,
            Title = "Edit a repeating event",
            Content = panel,
            PrimaryButtonText = "Save",
            CloseButtonText = "Cancel",
        };

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
