using E2E.Calendar.App.Controls;
using E2E.Calendar.App.Services;
using E2E.Calendar.App.Utilities;
using E2E.Calendar.App.ViewModels;
using E2E.Calendar.Core;
using E2E.Calendar.Core.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace E2E.Calendar.App.Views;

public partial class MonthView : Page
{
    private readonly EventEditorViewModel _editor;
    private readonly ReminderQuickAdd _reminders;
    private readonly List<MonthDayCell> _cells = new();
    private readonly List<TextBlock> _weekNumberLabels = new();
    private IReadOnlyList<MonthDay>? _builtDays;
    private bool? _builtShowWeekNumbers;

    public MonthView(
        MonthViewModel viewModel,
        EventEditorViewModel editor,
        ReminderQuickAdd reminders)
    {
        ViewModel = viewModel;
        _editor = editor;
        _reminders = reminders;

        InitializeComponent();

        ViewModel.PropertyChanged += OnViewModelPropertyChanged;
        ViewModel.PeriodChanged += OnPeriodChanged;

        EmptyStateView.ActionInvoked += OnEmptyStateAction;

        this.Loaded += OnLoaded;
    }

    public MonthViewModel ViewModel { get; }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        BuildGrid();
        await ViewModel.RefreshAsync();
    }

    private async void OnPeriodChanged(object? sender, EventArgs e) =>
        await Motion.SlideAndFadeInAsync(MonthHost);

    private async void OnEmptyStateAction(object? sender, EventArgs e)
    {
        var date = ViewModel.State.SelectedDate == default
            ? DateOnly.FromDateTime(DateTime.Now)
            : ViewModel.State.SelectedDate;

        var timeZone = TimeZoneInfo.Local;
        var startUtc = timeZone.ToUtc(date.ToDateTime(new TimeOnly(9, 0)));

        await _editor.OpenNewAsync(startUtc, startUtc.AddHours(1));
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MonthViewModel.Days) or nameof(MonthViewModel.ShowWeekNumbers))
        {
            BuildGrid();
        }
        else if (e.PropertyName == nameof(MonthViewModel.IsLoading))
        {
            Skeleton.IsActive = ViewModel.IsLoading;
        }
    }

    private void BuildGrid()
    {
        if (ReferenceEquals(_builtDays, ViewModel.Days) && _builtShowWeekNumbers == ViewModel.ShowWeekNumbers)
        {
            return;
        }

        _builtDays = ViewModel.Days;
        _builtShowWeekNumbers = ViewModel.ShowWeekNumbers;

        MonthHost.ColumnDefinitions.Clear();
        MonthHost.RowDefinitions.Clear();
        WeekdayHeaderHost.ColumnDefinitions.Clear();
        WeekdayHeaderHost.Children.Clear();
        WeekdayHeaderHost.RowDefinitions.Clear();

        foreach (var label in _weekNumberLabels)
        {
            MonthHost.Children.Remove(label);
        }

        _weekNumberLabels.Clear();

        var days = ViewModel.Days;

        if (days.Count == 0)
        {
            foreach (var cell in _cells)
            {
                cell.Visibility = Visibility.Collapsed;
            }

            EmptyStateView.Visibility = Visibility.Visible;
            return;
        }

        var hasEvents = days.Any(day => day.EventCount > 0);

        EmptyStateView.Visibility = hasEvents ? Visibility.Collapsed : Visibility.Visible;

        var column = 0;

        if (ViewModel.ShowWeekNumbers)
        {
            MonthHost.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(28) });
            WeekdayHeaderHost.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(28) });

            for (var week = 0; week < 6; week++)
            {
                var number = ViewModel.WeekNumbers.Count > week ? ViewModel.WeekNumbers[week] : 0;
                var label = new TextBlock
                {
                    Text = number.ToString(),
                    FontSize = 11,
                    Opacity = 0.6,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                };

                Grid.SetColumn(label, 0);
                Grid.SetRow(label, week);
                MonthHost.Children.Add(label);
                _weekNumberLabels.Add(label);
            }

            column = 1;
        }

        for (var index = 0; index < 7; index++)
        {
            MonthHost.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            WeekdayHeaderHost.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        }

        for (var row = 0; row < 6; row++)
        {
            MonthHost.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        }

        WeekdayHeaderHost.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        BuildWeekdayHeaders(column);

        EnsureCellCount(days.Count);

        for (var index = 0; index < days.Count; index++)
        {
            var cell = _cells[index];

            cell.Apply(days[index]);
            cell.Visibility = Visibility.Visible;

            Grid.SetColumn(cell, column + (index % 7));
            Grid.SetRow(cell, index / 7);
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
            MonthHost.Children.Remove(last);
            last.DaySelected -= OnDaySelected;
            last.NewEventRequested -= OnNewEventRequested;
            last.NewReminderRequested -= OnNewReminderRequested;
            last.ChipDropped -= OnChipDropped;
            _cells.RemoveAt(_cells.Count - 1);
        }

        while (_cells.Count < count)
        {
            var cell = new MonthDayCell();

            cell.DaySelected += OnDaySelected;
            cell.NewEventRequested += OnNewEventRequested;
            cell.NewReminderRequested += OnNewReminderRequested;
            cell.ChipDropped += OnChipDropped;

            MonthHost.Children.Add(cell);
            _cells.Add(cell);
        }
    }

    private void BuildWeekdayHeaders(int firstColumn)
    {
        var culture = System.Globalization.CultureInfo.CurrentCulture;
        var start = ViewModel.State.WeekStart;

        for (var index = 0; index < 7; index++)
        {
            var dayOfWeek = (DayOfWeek)(((int)start + index) % 7);
            var name = culture.DateTimeFormat.GetAbbreviatedDayName(dayOfWeek);
            var isWeekend = dayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;

            var label = new TextBlock
            {
                Text = name.ToUpper(culture),
                FontSize = 11,
                Opacity = isWeekend ? 0.6 : 0.85,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 6, 0, 6),
            };

            Grid.SetColumn(label, firstColumn + index);
            Grid.SetRow(label, 0);
            WeekdayHeaderHost.Children.Add(label);
        }
    }

    private void OnDaySelected(object? sender, DateOnly date) => ViewModel.SelectDay(date);

    private async void OnNewEventRequested(object? sender, DateOnly date)
    {
        var timeZone = TimeZoneInfo.Local;
        var startUtc = timeZone.ToUtc(date.ToDateTime(new TimeOnly(9, 0)));

        await _editor.OpenNewAsync(startUtc, startUtc.AddHours(1));
    }

    private async void OnNewReminderRequested(object? sender, DateOnly date)
    {
        var timeZone = TimeZoneInfo.Local;
        var dueUtc = timeZone.ToUtc(date.ToDateTime(new TimeOnly(9, 0)));

        var reminder = await _reminders.ShowAndCreateAsync(XamlRoot, dueUtc);

        if (reminder is not null)
        {
            await ViewModel.RefreshAsync();
        }
    }

    private async void OnChipDropped(object? sender, MonthChipDrop drop)
    {
        await ViewModel.MoveItemToDateAsync(drop.ItemId, drop.TargetDate);
        await ViewModel.RefreshAsync();
    }
}
