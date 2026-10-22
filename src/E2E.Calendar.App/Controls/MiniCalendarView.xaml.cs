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

        WeekdayHeader.ColumnDefinitions.Clear();
        WeekdayHeader.Children.Clear();
        DaysHost.ColumnDefinitions.Clear();
        DaysHost.RowDefinitions.Clear();

        for (var index = 0; index < MiniCalendarGridBuilder.DaysPerWeek; index++)
        {
            WeekdayHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(CellSize) });

            var header = new TextBlock
            {
                Width = CellSize,
                FontSize = 12,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                TextWrapping = TextWrapping.NoWrap,
            };

            if (index < ViewModel.WeekdayHeaders.Count)
            {
                header.Text = ViewModel.WeekdayHeaders[index];
                ToolTipService.SetToolTip(header, ViewModel.WeekdayHeaders[index]);
            }

            Grid.SetColumn(header, index);
            WeekdayHeader.Children.Add(header);
        }

        var rows = days.Count == 0 ? 1 : (int)Math.Ceiling(days.Count / (double)MiniCalendarGridBuilder.DaysPerWeek);

        for (var row = 0; row < rows; row++)
        {
            DaysHost.RowDefinitions.Add(new RowDefinition { Height = new GridLength(CellSize) });
        }

        for (var index = 0; index < MiniCalendarGridBuilder.DaysPerWeek; index++)
        {
            DaysHost.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(CellSize) });
        }

        EnsureCellCount(days.Count);

        for (var index = 0; index < days.Count; index++)
        {
            var cell = _cells[index];

            cell.Apply(days[index]);
            cell.Visibility = Visibility.Visible;

            Grid.SetColumn(cell, index % MiniCalendarGridBuilder.DaysPerWeek);
            Grid.SetRow(cell, index / MiniCalendarGridBuilder.DaysPerWeek);
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
            DaysHost.Children.Remove(last);
            last.Tapped -= OnDayCellTapped;
            _cells.RemoveAt(_cells.Count - 1);
        }

        while (_cells.Count < count)
        {
            var cell = new DayCell();

            cell.Tapped += OnDayCellTapped;

            DaysHost.Children.Add(cell);
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
        var delta = e.GetCurrentPoint(DaysHost).Properties.MouseWheelDelta;

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
