using E2E.Calendar.App.Services;
using E2E.Calendar.App.Utilities;
using E2E.Calendar.App.ViewModels;
using E2E.Calendar.Core;
using E2E.Calendar.Core.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace E2E.Calendar.App.Views;

public sealed partial class SettingsView : UserControl
{
    private readonly (string Name, FrameworkElement Panel)[] _sections;
    private readonly IDialogService _dialogs;

    public SettingsView(SettingsViewModel viewModel, IDialogService dialogs)
    {
        ArgumentNullException.ThrowIfNull(dialogs);

        ViewModel = viewModel;
        _dialogs = dialogs;

        InitializeComponent();

        _sections =
        [
            ("General", SectionGeneral),
            ("Accounts", SectionAccounts),
            ("Calendars", SectionCalendars),
            ("Notifications & sounds", SectionNotifications),
            ("Appearance", SectionAppearance),
            ("Shortcuts", SectionShortcuts),
            ("Premium", SectionPremium),
            ("Privacy & data", SectionPrivacy),
            ("Developer", SectionDeveloper),
            ("About", SectionAbout),
        ];

        SectionList.ItemsSource = ViewModel.Sections;
        SectionList.SelectedIndex = 0;

        Loaded += OnLoaded;
    }

    public SettingsViewModel ViewModel { get; }

    private async void OnLoaded(object sender, RoutedEventArgs e) => await ViewModel.LoadAsync();

    private async void OnBirthdaysToggled(object sender, RoutedEventArgs e)
    {
        if (sender is not ToggleSwitch { IsOn: true })
        {
            return;
        }

        try
        {
            await ViewModel.ImportBirthdaysAsync();
        }
        catch (Exception ex)
        {
            ViewModel.ShowBirthdays = false;
            var dialog = new ContentDialog
            {
                Title = "Birthdays could not be enabled",
                Content = ex.Message,
                CloseButtonText = "Close",
            };
            await _dialogs.ShowAsync(dialog);
        }
    }

    private void OnSectionSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var index = SectionList.SelectedIndex;

        if (index < 0 || index >= _sections.Length)
        {
            return;
        }

        for (var i = 0; i < _sections.Length; i++)
        {
            _sections[i].Panel.Visibility = i == index ? Visibility.Visible : Visibility.Collapsed;
        }

        ContentScroll.ChangeView(null, 0, null, disableAnimation: true);

        _ = Motion.FadeInAsync(ContentScroll);
    }

    private async void OnCreateCalendarClick(object sender, RoutedEventArgs e)
    {
        var nameBox = new TextBox { PlaceholderText = "Calendar name" };
        var dialog = new ContentDialog
        {
            Title = "Create calendar",
            Content = nameBox,
            PrimaryButtonText = "Create",
            CloseButtonText = "Cancel",
        };

        if (await _dialogs.ShowAsync(dialog) != ContentDialogResult.Primary ||
            string.IsNullOrWhiteSpace(nameBox.Text))
        {
            return;
        }

        await ViewModel.CreateCalendarAsync(nameBox.Text, CalendarPalette.Personal);
    }

    private async void OnDeleteCalendarClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string id })
        {
            return;
        }

        try
        {
            await ViewModel.DeleteCalendarAsync(id);
        }
        catch (InvalidOperationException ex)
        {
            await _dialogs.ShowAsync(new ContentDialog
            {
                Title = "Calendar cannot be deleted",
                Content = ex.Message,
                CloseButtonText = "Close",
            });
        }
    }

    private void OnCalendarColorClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string id } button)
        {
            return;
        }

        var flyout = new MenuFlyout();
        foreach (var color in CalendarPalette.All)
        {
            var item = new MenuFlyoutItem { Text = color };
            item.Click += async (_, _) => await ViewModel.SetCalendarColorAsync(id, color);
            flyout.Items.Add(item);
        }

        flyout.ShowAt(button);
    }

    private async void OnRemoveAccountClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string id } || id == "local")
        {
            return;
        }

        var dialog = new ContentDialog
        {
            Title = "Remove connected account?",
            Content = "This removes the account, its synced calendars, and their local event copies. The remote account is not changed.",
            PrimaryButtonText = "Remove",
            CloseButtonText = "Cancel",
        };

        if (await _dialogs.ShowAsync(dialog) == ContentDialogResult.Primary)
        {
            try
            {
                await ViewModel.RemoveAccountAsync(id);
            }
            catch (Exception ex)
            {
                await _dialogs.ShowAsync(new ContentDialog
                {
                    Title = "Account could not be removed",
                    Content = ex.Message,
                    CloseButtonText = "Close",
                });
            }
        }
    }
}
