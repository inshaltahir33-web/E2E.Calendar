using E2E.Calendar.App.Controls;
using E2E.Calendar.App.Services;
using E2E.Calendar.App.Utilities;
using E2E.Calendar.App.ViewModels;
using E2E.Calendar.Core.Abstractions;
using E2E.Calendar.Core.Models;
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
        EventDeletionCoordinator deletions,
        ReminderQuickAdd reminders,
        EventDetailsPresenter details)
    {
        ViewModel = viewModel;
        _editor = editor;
        _events = events;
        _eventService = eventService;

        InitializeComponent();

        TimeGridInteractions.Wire(TimeGrid, _editor, _events, _eventService, deletions, reminders, details, ViewModel.RefreshAsync);

        TimeGrid.DayHeaderClicked += (_, date) =>
        {
            ViewModel.State.SelectDateCommand.Execute(date);
            ViewModel.State.SetViewModeCommand.Execute(CalendarViewMode.Day);
        };

        TimeGrid.ItemCompleted += async (_, args) =>
        {
            await ViewModel.ToggleCompletedAsync(args.Item);
            await ViewModel.RefreshAsync();
        };

        ViewModel.PeriodChanged += OnPeriodChanged;
        ViewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    public WeekViewModel ViewModel { get; }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await ViewModel.RefreshAsync();
        await ViewModel.RefreshWeatherAsync();
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
