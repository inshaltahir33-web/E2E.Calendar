using E2E.Calendar.App.Services;
using E2E.Calendar.App.Utilities;
using E2E.Calendar.App.ViewModels;
using E2E.Calendar.Core.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Text;
using Windows.System;
using DispatcherQueue = Microsoft.UI.Dispatching.DispatcherQueue;
using XamlAutomationProperties = Microsoft.UI.Xaml.Automation.AutomationProperties;
using UiDispatcherQueueTimer = Microsoft.UI.Dispatching.DispatcherQueueTimer;

namespace E2E.Calendar.App.Controls;

public sealed partial class MiniCalendarView : UserControl
{
    private const double CellSize = 36;
    private const double HeaderHeight = 16;

    private readonly MiniCalendarViewModel _viewModel;
    private readonly IDialogService _dialogs;
    private readonly List<DayCell> _cells = new();
    private readonly UiDispatcherQueueTimer _wheelDebounce;
    private IReadOnlyList<MiniCalendarDayViewModel>? _builtDays;
    private int _pendingWheelDirection;
    private bool _isLoaded;

    public MiniCalendarView(MiniCalendarViewModel viewModel, IDialogService dialogs)
    {
        ArgumentNullException.ThrowIfNull(dialogs);

        ViewModel = viewModel;
        _viewModel = viewModel;
        _dialogs = dialogs;

        InitializeComponent();

        ViewModel.PropertyChanged += OnViewModelPropertyChanged;
        _wheelDebounce = DispatcherQueue.GetForCurrentThread().CreateTimer();
        _wheelDebounce.IsRepeating = false;
        _wheelDebounce.Interval = TimeSpan.FromMilliseconds(150);
        _wheelDebounce.Tick += OnWheelDebounceTick;

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
        _isLoaded = true;
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
                FontSize = Tokens.Double("FontSizeCaption", 12d),
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

    private void OnPreviousMonthClick(object sender, RoutedEventArgs e)
    {
        _viewModel.MoveMonth(-1);
    }

    private void OnNextMonthClick(object sender, RoutedEventArgs e)
    {
        _viewModel.MoveMonth(1);
    }

    private async void OnHeaderClick(object sender, RoutedEventArgs e)
    {
        var year = ViewModel.VisibleYear;
        var month = ViewModel.VisibleMonth;
        var dialog = new ContentDialog
        {
            Title = "Choose month and year",
            PrimaryButtonText = "Go to month",
            CloseButtonText = "Cancel",
        };

        var yearText = new TextBlock
        {
            Text = year.ToString(),
            HorizontalAlignment = HorizontalAlignment.Center,
            FontSize = Tokens.Double("FontSizeGlyphLarge", 18d),
            FontWeight = FontWeights.SemiBold,
        };
        var months = new Grid { ColumnSpacing = 6, RowSpacing = 6 };
        for (var column = 0; column < 3; column++)
        {
            months.ColumnDefinitions.Add(new ColumnDefinition());
        }

        for (var index = 0; index < 12; index++)
        {
            var monthNumber = index + 1;
            var button = new Button
            {
                Content = new DateTime(year, monthNumber, 1).ToString("MMM"),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Tag = monthNumber,
            };
            button.Click += (_, _) =>
            {
                month = (int)button.Tag;
                foreach (var child in months.Children)
                {
                    if (child is Button other)
                    {
                    }
                }
            };
            Grid.SetColumn(button, index % 3);
            Grid.SetRow(button, index / 3);
            months.Children.Add(button);
        }

        for (var row = 0; row < 4; row++)
        {
            months.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        }

        var previousYear = new Button { Content = "‹" };
        var nextYear = new Button { Content = "›" };
        XamlAutomationProperties.SetName(previousYear, "Previous year");
        XamlAutomationProperties.SetName(nextYear, "Next year");
        previousYear.Click += (_, _) =>
        {
            year--;
            yearText.Text = year.ToString();
            UpdateMonthLabels(months, year);
        };
        nextYear.Click += (_, _) =>
        {
            year++;
            yearText.Text = year.ToString();
            UpdateMonthLabels(months, year);
        };

        var yearBar = new Grid();
        yearBar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        yearBar.ColumnDefinitions.Add(new ColumnDefinition());
        yearBar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(yearText, 1);
        Grid.SetColumn(previousYear, 0);
        Grid.SetColumn(nextYear, 2);
        yearBar.Children.Add(previousYear);
        yearBar.Children.Add(yearText);
        yearBar.Children.Add(nextYear);

        var content = new StackPanel { Spacing = 8, Children = { yearBar, months } };
        dialog.Content = content;

        if (await _dialogs.ShowAsync(dialog) == ContentDialogResult.Primary)
        {
            var target = new DateOnly(year, month, 1);
            var delta = (target.Year - ViewModel.VisibleYear) * 12 + target.Month - ViewModel.VisibleMonth;
            await ChangeMonthAsync(delta);
        }
    }

    private static void UpdateMonthLabels(Grid months, int year)
    {
        foreach (var child in months.Children)
        {
            if (child is Button button && button.Tag is int month)
            {
                button.Content = new DateTime(year, month, 1).ToString("MMM");
            }
        }
    }

    private async Task ChangeMonthAsync(int delta)
    {
        if (delta == 0)
        {
            return;
        }

        var from = new DateOnly(ViewModel.VisibleYear, ViewModel.VisibleMonth, 1);
        var target = from.AddMonths(delta);
        var navDirection = NavigationDirection.Get(from, target);
        var direction = (int)navDirection;

        if (direction == 0)
        {
            return;
        }
        for (var index = 0; index < Math.Abs(delta); index++)
        {
            if (direction < 0)
            {
                _viewModel.PreviousMonthCommand.Execute(null);
            }
            else
            {
                _viewModel.NextMonthCommand.Execute(null);
            }

            await _viewModel.RefreshAsync();
            await AnimateMonthAsync(direction);
        }
    }

    private async Task AnimateMonthAsync(int direction)
    {
        if (!_isLoaded)
        {
            return;
        }

        var transform = new TranslateTransform { X = direction > 0 ? CalendarGrid.ActualWidth : -CalendarGrid.ActualWidth };
        CalendarGrid.RenderTransform = transform;
        var animation = new DoubleAnimation
        {
            To = 0,
            Duration = TimeSpan.FromMilliseconds(180),
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
        };
        Storyboard.SetTarget(animation, transform);
        Storyboard.SetTargetProperty(animation, "X");
        var storyboard = new Storyboard();
        storyboard.Children.Add(animation);
        storyboard.Begin();
        await Task.Delay(180);
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

    private void OnCalendarPointerWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        var delta = e.GetCurrentPoint(CalendarGrid).Properties.MouseWheelDelta;

        if (delta is 0)
        {
            return;
        }

        e.Handled = true;
        _pendingWheelDirection = delta > 0 ? -1 : 1;
        _wheelDebounce.Stop();
        _wheelDebounce.Start();
    }

    private async void OnWheelDebounceTick(UiDispatcherQueueTimer sender, object args)
    {
        _wheelDebounce.Stop();
        var direction = _pendingWheelDirection;
        _pendingWheelDirection = 0;
        await ChangeMonthAsync(direction);
    }
}
