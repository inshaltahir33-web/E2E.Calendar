using E2E.Calendar.App;
using E2E.Calendar.App.Services;
using E2E.Calendar.App.Utilities;
using E2E.Calendar.App.ViewModels;
using E2E.Calendar.Core;
using E2E.Calendar.Core.Models;
using E2E.Calendar.Sync.Ics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.System;
using WinRT.Interop;

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

        BirthdayImportCard.Visibility = AppFeatures.SyncUiEnabled ? Visibility.Visible : Visibility.Collapsed;
        SectionAccounts.Visibility = Visibility.Collapsed;

        // The panel array must mirror SettingsViewModel.Sections index for index, because the
        // selected index drives which panel is shown. Developer only exists when E2E_DEMO=1.
        var sections = new List<(string Name, FrameworkElement Panel)>
        {
            ("General", SectionGeneral),
            ("Accounts", SectionAccounts),
        };

        sections.Add(("Calendars", SectionCalendars));
        sections.Add(("Templates", SectionTemplates));
        sections.Add(("Notifications", SectionNotifications));
        sections.Add(("Appearance", SectionAppearance));
        sections.Add(("Shortcuts", SectionShortcuts));
        sections.Add(("Premium", SectionPremium));
        sections.Add(("Privacy & data", SectionPrivacy));

        if (AppFeatures.DemoEnabled)
        {
            sections.Add(("Developer", SectionDeveloper));
        }

        sections.Add(("About", SectionAbout));

        _sections = sections.ToArray();

        SectionList.ItemsSource = ViewModel.Sections;
        SectionList.SelectedIndex = 0;

        Loaded += OnLoaded;
    }

    public SettingsViewModel ViewModel { get; }

    private async void OnLoaded(object sender, RoutedEventArgs e) => await ViewModel.LoadAsync();

    private async void OnDeleteAllDataClick(object sender, RoutedEventArgs e)
    {
        var dialog = new ContentDialog
        {
            Title = "Delete all data?",
            Content = "This removes every calendar, event, reminder, task, account and setting from this device. This cannot be undone.",
            PrimaryButtonText = "Delete all",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
        };

        if (await _dialogs.ShowAsync(dialog) != ContentDialogResult.Primary)
        {
            return;
        }

        try
        {
            await ViewModel.DeleteAllDataAsync();
        }
        catch (Exception ex)
        {
            await _dialogs.ShowAsync(new ContentDialog
            {
                Title = "Data could not be deleted",
                Content = ex.Message,
                CloseButtonText = "Close",
            });
        }
    }

    private async void OnExportEventsClick(object sender, RoutedEventArgs e)
    {
        if (App.Current.MainWindow is not { } window)
        {
            return;
        }

        var picker = new FileSavePicker { SuggestedFileName = "calendar" };
        picker.FileTypeChoices.Add("iCalendar file", new List<string> { ".ics" });
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(window));

        var file = await picker.PickSaveFileAsync();

        if (file is null)
        {
            return;
        }

        try
        {
            await FileIO.WriteTextAsync(file, await ViewModel.ExportEventsAsync());
        }
        catch (Exception ex)
        {
            await _dialogs.ShowAsync(new ContentDialog
            {
                Title = "Events could not be exported",
                Content = ex.Message,
                CloseButtonText = "Close",
            });
        }
    }

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

    private async void OnAddSubscriptionClick(object sender, RoutedEventArgs e)
    {
        var nameBox = new TextBox
        {
            Header = "Calendar name",
            PlaceholderText = "Team holidays",
        };
        var urlBox = new TextBox
        {
            Header = "Calendar URL",
            PlaceholderText = "https://example.com/calendar.ics",
        };
        var intervalBox = new NumberBox
        {
            Header = "Refresh every (hours)",
            Minimum = IcsSubscriptionService.MinRefreshHours,
            Maximum = IcsSubscriptionService.MaxRefreshHours,
            Value = 6,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact,
        };

        var content = new StackPanel { Spacing = 12 };
        content.Children.Add(nameBox);
        content.Children.Add(urlBox);
        content.Children.Add(intervalBox);

        var dialog = new ContentDialog
        {
            Title = "Subscribe to a calendar",
            Content = content,
            PrimaryButtonText = "Subscribe",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
        };

        if (await _dialogs.ShowAsync(dialog) != ContentDialogResult.Primary)
        {
            return;
        }

        var hours = double.IsNaN(intervalBox.Value)
            ? IcsSubscriptionService.MinRefreshHours
            : (int)Math.Round(intervalBox.Value);

        try
        {
            await ViewModel.AddSubscriptionAsync(urlBox.Text, nameBox.Text, hours);
        }
        catch (Exception ex)
        {
            await _dialogs.ShowAsync(new ContentDialog
            {
                Title = "Subscription could not be added",
                Content = ex.Message,
                CloseButtonText = "Close",
            });
        }
    }

    private async void OnDeleteTemplateClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string id })
        {
            await ViewModel.DeleteTemplateAsync(id);
        }
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

        OverlayFocus.ShowAt(flyout, button);
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

    private async void OnOpenStartupSettingsClick(object sender, RoutedEventArgs e)
    {
        if (!await Launcher.LaunchUriAsync(new Uri("ms-settings:startupapps")))
        {
            await _dialogs.ShowAsync(new ContentDialog
            {
                Title = "Windows startup settings could not be opened",
                Content = "Open Settings > Apps > Startup manually.",
                CloseButtonText = "Close",
            });
        }
    }

    private async void OnWindowsNotificationSettingsClick(object sender, RoutedEventArgs e)
    {
        if (!await Launcher.LaunchUriAsync(new Uri("ms-settings:notifications")))
        {
            await _dialogs.ShowAsync(new ContentDialog
            {
                Title = "Windows notification settings could not be opened",
                Content = "Open Settings > System > Notifications manually.",
                CloseButtonText = "Close",
            });
        }
    }
}
