using E2E.Calendar.App.Utilities;
using E2E.Calendar.App.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace E2E.Calendar.App.Views;

public sealed partial class SettingsView : UserControl
{
    private readonly (string Name, FrameworkElement Panel)[] _sections;

    public SettingsView(SettingsViewModel viewModel)
    {
        ViewModel = viewModel;

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
                XamlRoot = XamlRoot,
            };
            await dialog.ShowAsync();
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
}
