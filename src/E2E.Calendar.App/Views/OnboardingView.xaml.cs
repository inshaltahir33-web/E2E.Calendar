using E2E.Calendar.Core.Models;
using E2E.Calendar.Core.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;

namespace E2E.Calendar.App.Views;

/// <summary>
/// Three-step, skippable first-run experience: connect an account, pick a theme,
/// enable notifications. The chosen values are returned to the caller, which
/// persists them via <c>SettingsService</c>.
/// </summary>
public sealed partial class OnboardingView : UserControl
{
    private readonly StackPanel[] _steps;
    private int _step;

    public OnboardingView()
    {
        InitializeComponent();

        _steps = [StepConnect, StepTheme, StepNotifications];

        UpdateStep();
    }

    public AppTheme SelectedTheme { get; private set; } = AppTheme.System;

    public bool NotificationsEnabled { get; private set; } = true;

    public bool Skipped { get; private set; }

    public event EventHandler? Completed;

    public static async Task ShowAsync(XamlRoot? xamlRoot, SettingsService settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        if (xamlRoot is null)
        {
            return;
        }

        var view = new OnboardingView();

        var dialog = new ContentDialog
        {
            XamlRoot = xamlRoot,
            Content = view,
            CloseButtonText = "Skip",
            PrimaryButtonText = "Continue",
            DefaultButton = ContentDialogButton.Primary,
        };

        AutomationProperties.SetName(dialog, "Welcome to E2E Calendar");

        view.Completed += (_, _) =>
        {
            var current = settings.Current;

            _ = settings.UpdateAsync(current with
            {
                Theme = view.SelectedTheme,
                OnboardingComplete = true,
            });

            dialog.Hide();
        };

        var result = await dialog.ShowAsync();

        if (result == ContentDialogResult.None)
        {
            var current = settings.Current;

            _ = settings.UpdateAsync(current with { OnboardingComplete = true });
        }
    }

    private void OnNextClick(object sender, RoutedEventArgs e)
    {
        if (_step < _steps.Length - 1)
        {
            _step++;
            UpdateStep();

            return;
        }

        CaptureChoices();
        Completed?.Invoke(this, EventArgs.Empty);
    }

    private void OnSkipClick(object sender, RoutedEventArgs e)
    {
        Skipped = true;
        CaptureChoices();
        Completed?.Invoke(this, EventArgs.Empty);
    }

    private void CaptureChoices()
    {
        SelectedTheme = GetSelectedTheme();
        NotificationsEnabled = EnableNotifications.IsChecked == true;
    }

    private AppTheme GetSelectedTheme()
    {
        foreach (var child in ((StackPanel)StepTheme.Children[2]).Children)
        {
            if (child is RadioButton { IsChecked: true, Tag: string tag } &&
                Enum.TryParse<AppTheme>(tag, out var theme))
            {
                return theme;
            }
        }

        return AppTheme.System;
    }

    private void UpdateStep()
    {
        for (var i = 0; i < _steps.Length; i++)
        {
            _steps[i].Visibility = i == _step ? Visibility.Visible : Visibility.Collapsed;
        }

        StepIndicator.Text = $"{_step + 1} of {_steps.Length}";

        var label = _step == _steps.Length - 1 ? "Get started" : "Continue";
        NextButton.Content = label;
        AutomationProperties.SetName(NextButton, label);
    }
}
