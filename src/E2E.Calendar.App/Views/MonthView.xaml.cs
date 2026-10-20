using E2E.Calendar.App.Controls;
using E2E.Calendar.App.ViewModels;
using E2E.Calendar.Core;
using E2E.Calendar.Core.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace E2E.Calendar.App.Views;

public partial class MonthView : Page
{
    private readonly EventEditorViewModel _editor;
    private readonly List<MonthDayCell> _cells = new();

    public MonthView(
        MonthViewModel viewModel,
        EventEditorViewModel editor)
    {
        ViewModel = viewModel;
        _editor = editor;

        InitializeComponent();

        ViewModel.Days.CollectionChanged += OnDaysChanged;
        ViewModel.PropertyChanged += OnViewModelPropertyChanged;

        this.Loaded += OnLoaded;
    }

    public MonthViewModel ViewModel { get; }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await ViewModel.RefreshAsync();
        BuildGrid();
    }

    private void OnDaysChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e) =>
        BuildGrid();

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MonthViewModel.ShowWeekNumbers))
        {
            BuildGrid();
        }
    }

    private void BuildGrid()
    {
        _cells.Clear();
        MonthHost.Children.Clear();
        MonthHost.ColumnDefinitions.Clear();
        MonthHost.RowDefinitions.Clear();
        WeekdayHeaderHost.Children.Clear();
        WeekdayHeaderHost.ColumnDefinitions.Clear();
        WeekdayHeaderHost.RowDefinitions.Clear();

        var days = ViewModel.Days;

        if (days.Count == 0)
        {
            EmptyState.Visibility = Visibility.Visible;
            return;
        }

        EmptyState.Visibility = Visibility.Collapsed;

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

        for (var index = 0; index < days.Count; index++)
        {
            var day = days[index];
            var cell = new MonthDayCell
            {
                DayNumber = day.DayNumber.ToString(),
                IsToday = day.IsToday,
                IsCurrentMonth = day.IsCurrentMonth,
                IsWeekend = day.IsWeekend,
                IsSelected = day.IsSelected,
                MoreLabel = day.MoreLabel,
                MoreVisible = day.MoreCount > 0,
                EventCount = day.EventCount,
                Date = day.Date,
            };

            cell.SetChips(day.Chips);
            cell.DaySelected += OnDaySelected;
            cell.NewEventRequested += OnNewEventRequested;
            cell.ChipDropped += OnChipDropped;

            Grid.SetColumn(cell, column + (index % 7));
            Grid.SetRow(cell, index / 7);

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
            var day = (int)start + index;
            var name = culture.DateTimeFormat.GetAbbreviatedDayName((DayOfWeek)((day % 7 + 7) % 7));
            var isWeekend = day is 0 or 6;

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

    private async void OnChipDropped(object? sender, MonthChipDrop drop)
    {
        await ViewModel.MoveItemToDateAsync(drop.ItemId, drop.TargetDate);
        await ViewModel.RefreshAsync();
        BuildGrid();
    }
}
