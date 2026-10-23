using E2E.Calendar.App.ViewModels;
using E2E.Calendar.Core.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.System;

namespace E2E.Calendar.App.Controls;

public sealed partial class MiniCalendarView : UserControl
{
    private const double CellSize = 36;
    private const double HeaderHeight = 16;

    private readonly MiniCalendarViewModel _viewModel;
    private readonly List<DayCell> _cells = new();
    private IReadOnlyList<MiniCalendarDayViewModel>? _builtDays;

    public MiniCalendarView(MiniCalendarViewModel viewModel)
    {
        ViewModel = viewModel;
        _viewModel = viewModel;

        InitializeComponent();

        ViewModel.PropertyChanged += OnViewModelPropertyChanged;

        this.Loaded += OnLoaded;
    }

    public MiniCalendarViewModel ViewModel { get; }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MiniCalendarViewModel.Days) or nameof(MiniCalendarViewModel.WeekdayHeaders))
        {
            BuildGrid();
        }
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await _viewModel.RefreshAsync();
        BuildGrid();
    }

    private void BuildGrid()
    {
        if (ReferenceEquals(_builtDays, ViewModel.Days))
        {
            return;
        }

        _builtDays = ViewModel.Days;

        var days = ViewModel.Days;

        CalendarGrid.ColumnDefinitions.Clear();
        CalendarGrid.RowDefinitions.Clear();
        CalendarGrid.Children.Clear();

        // Header and day cells live in the same grid so the seven columns can
        // never drift out of alignment (the old split header clipped letters).
        for (var index = 0; index < MiniCalendarGridBuilder.DaysPerWeek; index++)
        {
            CalendarGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(CellSize) });
        }

        CalendarGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(HeaderHeight) });

        for (var index = 0; index < MiniCalendarGridBuilder.DaysPerWeek; index++)
        {
            var header = new TextBlock
            {
                FontSize = 12,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.NoWrap,
            };

            if (index < ViewModel.WeekdayHeaders.Count)
            {
                header.Text = ViewModel.WeekdayHeaders[index];
                ToolTipService.SetToolTip(header, ViewModel.WeekdayHeaders[index]);
            }

            Grid.SetColumn(header, index);
            Grid.SetRow(header, 0);
            CalendarGrid.Children.Add(header);
        }

        var rows = days.Count == 0 ? 1 : (int)Math.Ceiling(days.Count / (double)MiniCalendarGridBuilder.DaysPerWeek);

        for (var row = 0; row < rows; row++)
        {
            CalendarGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(CellSize) });
        }

        EnsureCellCount(days.Count);

        for (var index = 0; index < days.Count; index++)
        {
            var cell = _cells[index];

            cell.Apply(days[index]);
            cell.Visibility = Visibility.Visible;

            if (!CalendarGrid.Children.Contains(cell))
            {
                CalendarGrid.Children.Add(cell);
            }

            Grid.SetColumn(cell, index % MiniCalendarGridBuilder.DaysPerWeek);
            Grid.SetRow(cell, (index / MiniCalendarGridBuilder.DaysPerWeek) + 1);
        }

        for (var index = days.Count; index < _cells.Count; index++)
        {
            _cells[index].Visibility = Visibility.Collapsed;
        }
    }

    private void EnsureCellCount(int count)
    {
        while (_cells.Count > count)
        {
            var last = _cells[^1];
            CalendarGrid.Children.Remove(last);
            last.Tapped -= OnDayCellTapped;
            _cells.RemoveAt(_cells.Count - 1);
        }

        while (_cells.Count < count)
        {
            var cell = new DayCell();

            cell.Tapped += OnDayCellTapped;

            CalendarGrid.Children.Add(cell);
            _cells.Add(cell);
        }
    }

    private void OnDayCellTapped(object sender, TappedRoutedEventArgs e)
    {
        if (sender is FrameworkElement element && element.Tag is MiniCalendarDayViewModel day)
        {
            _viewModel.SelectDayCommand.Execute(day);
        }
    }

    private async void OnPreviousMonthClick(object sender, RoutedEventArgs e)
    {
        _viewModel.PreviousMonthCommand.Execute(null);
        await _viewModel.RefreshAsync();
    }

    private async void OnNextMonthClick(object sender, RoutedEventArgs e)
    {
        _viewModel.NextMonthCommand.Execute(null);
        await _viewModel.RefreshAsync();
    }

    private async void OnCalendarKeyDown(object sender, KeyRoutedEventArgs e)
    {
        switch (e.Key)
        {
            case VirtualKey.Left:
                _viewModel.MoveSelectionCommand.Execute(-1);
                break;

            case VirtualKey.Right:
                _viewModel.MoveSelectionCommand.Execute(1);
                break;

            case VirtualKey.Up:
                _viewModel.MoveSelectionCommand.Execute(-7);
                break;

            case VirtualKey.Down:
                _viewModel.MoveSelectionCommand.Execute(7);
                break;

            case VirtualKey.PageUp:
                _viewModel.PreviousMonthCommand.Execute(null);
                break;

            case VirtualKey.PageDown:
                _viewModel.NextMonthCommand.Execute(null);
                break;

            case VirtualKey.Home:
                _viewModel.MoveToFirstOfMonthCommand.Execute(null);
                break;

            default:
                return;
        }

        e.Handled = true;
        await _viewModel.RefreshAsync();
    }

    private async void OnCalendarPointerWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        var delta = e.GetCurrentPoint(CalendarGrid).Properties.MouseWheelDelta;

        if (delta is 0)
        {
            return;
        }

        e.Handled = true;

        if (delta > 0)
        {
            _viewModel.PreviousMonthCommand.Execute(null);
        }
        else
        {
            _viewModel.NextMonthCommand.Execute(null);
        }

        await _viewModel.RefreshAsync();
    }
}
