using E2E.Calendar.App.Controls;
using E2E.Calendar.App.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace E2E.Calendar.App.Views;

public partial class WeekView : Page
{
    private readonly EventEditorViewModel _editor;

    public WeekView(WeekViewModel viewModel, EventEditorViewModel editor)
    {
        ViewModel = viewModel;
        _editor = editor;

        InitializeComponent();

        TimeGridInteractions.Wire(TimeGrid, _editor, ViewModel.RefreshAsync);
    }

    public WeekViewModel ViewModel { get; }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await ViewModel.RefreshAsync();
    }
}
