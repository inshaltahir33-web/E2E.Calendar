using E2E.Calendar.App.Services;
using E2E.Calendar.App.ViewModels;
using E2E.Calendar.Core;
using E2E.Calendar.Core.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;

namespace E2E.Calendar.App.Views;

public partial class MonthView : Page
{
    private readonly CalendarStateViewModel _state;
    private readonly NavigationService _navigation;

    public MonthView(
        MonthViewModel viewModel,
        CalendarStateViewModel state,
        NavigationService navigation)
    {
        ViewModel = viewModel;
        _state = state;
        _navigation = navigation;

        InitializeComponent();
    }

    public MonthViewModel ViewModel { get; }

    private void OnDayNumberClicked(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement element && element.DataContext is MonthDay day)
        {
            OpenDay(day);
        }
    }

    private void OnMoreClicked(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement element || element.DataContext is not MonthDay day)
        {
            return;
        }

        var list = new ListView
        {
            ItemsSource = day.AllItems,
            SelectionMode = ListViewSelectionMode.None,
            MaxHeight = 320,
        };

        AutomationProperties.SetName(list, $"All events on {day.Date:yyyy-MM-dd}");

        var flyout = new Flyout
        {
            Content = list,
            Placement = FlyoutPlacementMode.BottomEdgeAlignedRight,
        };

        flyout.ShowAt(element);
    }

    private void OpenDay(MonthDay day)
    {
        var local = day.Date.ToDateTime(TimeOnly.MinValue);

        _state.CurrentDate = TimeZoneInfo.Local.ToUtc(local);
        _navigation.NavigateTo(NavigationKeys.Day);
    }
}
