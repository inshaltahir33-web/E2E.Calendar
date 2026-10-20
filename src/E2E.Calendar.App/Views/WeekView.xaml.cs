using E2E.Calendar.App.Controls;
using E2E.Calendar.App.ViewModels;
using E2E.Calendar.Core.Abstractions;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace E2E.Calendar.App.Views;

public partial class WeekView : Page
{
    private readonly EventEditorViewModel _editor;
    private readonly IEventRepository _events;
    private readonly IEventService _eventService;

    public WeekView(
        WeekViewModel viewModel,
        EventEditorViewModel editor,
        IEventRepository events,
        IEventService eventService)
    {
        ViewModel = viewModel;
        _editor = editor;
        _events = events;
        _eventService = eventService;

        InitializeComponent();

        TimeGridInteractions.Wire(TimeGrid, _editor, _events, _eventService, ViewModel.RefreshAsync);
    }

    public WeekViewModel ViewModel { get; }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await ViewModel.RefreshAsync();
    }
}
