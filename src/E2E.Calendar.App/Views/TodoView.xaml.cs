using E2E.Calendar.App.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace E2E.Calendar.App.Views;

public sealed partial class TodoView : Page
{
    public TodoView(TodoViewModel viewModel, string? filter = null)
    {
        ViewModel = viewModel;
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            await ViewModel.LoadAsync(filter);
            EmptyState.Visibility = ViewModel.Items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        };
    }

    public TodoViewModel ViewModel { get; }

    private async void OnItemCheckClick(object sender, RoutedEventArgs e)
    {
        if (sender is CheckBox { Tag: TodoRow row })
        {
            await ViewModel.ToggleAsync(row);
            EmptyState.Visibility = ViewModel.Items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }
    }
}
