using E2E.Calendar.App.Controls;
using E2E.Calendar.App.Services;
using E2E.Calendar.App.Utilities;
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
        IEventService eventService,
        ReminderQuickAdd reminders)
    {
        ViewModel = viewModel;
        _editor = editor;
        _events = events;
        _eventService = eventService;

        InitializeComponent();

        TimeGridInteractions.Wire(TimeGrid, _editor, _events, _eventService, reminders, ViewModel.RefreshAsync);

        ViewModel.PeriodChanged += OnPeriodChanged;
        ViewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    public WeekViewModel ViewModel { get; }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await ViewModel.RefreshAsync();
    }

    private async void OnPeriodChanged(object? sender, EventArgs e) =>
        await Motion.SlideAndFadeInAsync(TimeGrid);

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(WeekViewModel.IsLoading))
        {
            Skeleton.IsActive = ViewModel.IsLoading;
        }
    }
}
