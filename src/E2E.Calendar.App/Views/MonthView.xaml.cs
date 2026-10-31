using E2E.Calendar.App.Controls;
using E2E.Calendar.App.Services;
using E2E.Calendar.App.Utilities;
using E2E.Calendar.App.ViewModels;
using E2E.Calendar.Core;
using E2E.Calendar.Core.Models;
using E2E.Calendar.Core.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace E2E.Calendar.App.Views;

public partial class MonthView : Page
{
    private readonly EventEditorViewModel _editor;
    private readonly ReminderQuickAdd _reminders;
    private readonly WeekViewModel _weekViewModel;
    private readonly EventDetailsPresenter _details;
    private const double BannerLineTop = 22d;
    private const double BannerCornerRadius = 4d;

    private readonly List<MonthDayCell> _cells = new();
    private readonly List<TextBlock> _weekNumberLabels = new();
    private readonly List<PlacedBanner> _bannerBars = new();
    private readonly List<Canvas> _bannerLayers = new();
    private IReadOnlyList<MonthDay>? _builtDays;
    private bool? _builtShowWeekNumbers;

    public MonthView(
        MonthViewModel viewModel,
        WeekViewModel weekViewModel,
        EventEditorViewModel editor,
        ReminderQuickAdd reminders,
        EventDetailsPresenter details)
    {
        ViewModel = viewModel;
        _weekViewModel = weekViewModel;
        _editor = editor;
        _reminders = reminders;
        _details = details;

        InitializeComponent();

        ViewModel.PropertyChanged += OnViewModelPropertyChanged;
        _weekViewModel.PropertyChanged += OnWeekViewModelPropertyChanged;
        ViewModel.PeriodChanged += OnPeriodChanged;

        EmptyStateView.ActionInvoked += OnEmptyStateAction;

        MonthHost.SizeChanged += (_, _) => PositionBanners();

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
        if (e.PropertyName is nameof(MonthViewModel.Days))
        {
            BuildGrid();
        }
        else if (e.PropertyName == nameof(MonthViewModel.IsLoading))
        {
            Skeleton.IsActive = ViewModel.IsLoading;
        }
    }

    private void OnWeekViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(WeekViewModel.ShowWeekNumbers))
        {
            BuildGrid();
        }
    }

    private void BuildGrid()
    {
        if (ReferenceEquals(_builtDays, ViewModel.Days) && _builtShowWeekNumbers == _weekViewModel.ShowWeekNumbers)
        {
            return;
        }

        _builtDays = ViewModel.Days;
        _builtShowWeekNumbers = _weekViewModel.ShowWeekNumbers;

        RemoveBannerLayer();
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

        if (_weekViewModel.ShowWeekNumbers)
        {
            MonthHost.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(28) });
            WeekdayHeaderHost.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(28) });

            for (var week = 0; week < 6; week++)
            {
                var number = ViewModel.WeekNumbers.Count > week ? ViewModel.WeekNumbers[week] : 0;
                var label = new TextBlock
                {
                    Text = number.ToString(),
                    FontSize = Tokens.Double("FontSizeSectionHeader", 11d),
                    Opacity = 0.6,
                    Foreground = ThemeBrushes.Get(this, "App.TextTertiaryBrush"),
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

        var lastRow = (days.Count - 1) / 7;

        for (var index = 0; index < days.Count; index++)
        {
            var cell = _cells[index];
            var dayColumn = index % 7;
            var dayRow = index / 7;

            cell.Apply(days[index]);
            cell.Visibility = Visibility.Visible;

            Grid.SetColumn(cell, column + dayColumn);
            Grid.SetRow(cell, dayRow);

            // Outer edges are dropped so the 1px grid never doubles up with the card border.
            cell.SetGridEdges(right: dayColumn < 6, bottom: dayRow < lastRow);
        }

        for (var index = days.Count; index < _cells.Count; index++)
        {
            _cells[index].Visibility = Visibility.Collapsed;
        }

        RenderBanners(column);
    }

    /// <summary>
    /// Draws one bar per all-day/multi-day item across the day cells of its week row.
    /// The canvas is hit-test transparent so taps, double-taps and drops still reach the cells.
    /// </summary>
    private void RenderBanners(int firstColumn)
    {
        var weeks = ViewModel.Weeks;

        if (weeks.Count == 0)
        {
            return;
        }

        var layers = new Canvas[6];

        for (var row = 0; row < layers.Length; row++)
        {
            var canvas = new Canvas { IsHitTestVisible = false };

            Grid.SetRow(canvas, row);
            Grid.SetColumn(canvas, 0);
            Grid.SetColumnSpan(canvas, firstColumn + 7);
            Canvas.SetZIndex(canvas, 1);
            canvas.SizeChanged += (_, e) => canvas.Clip = new Microsoft.UI.Xaml.Media.RectangleGeometry
            {
                Rect = new Windows.Foundation.Rect(0, 0, e.NewSize.Width, e.NewSize.Height),
            };

            MonthHost.Children.Add(canvas);
            _bannerLayers.Add(canvas);
            layers[row] = canvas;
        }

        foreach (var week in weeks)
        {
            if (week.Row < 0 || week.Row >= 6)
            {
                continue;
            }

            var canvas = layers[week.Row];

            foreach (var banner in week.Banners)
            {
                var bar = CreateBannerBar(banner);
                canvas.Children.Add(bar);
                _bannerBars.Add(new PlacedBanner(bar, banner, week.Row, canvas));
            }
        }

        MonthHost.UpdateLayout();
        PositionBanners();
    }

    private void PositionBanners()
    {
        if (_bannerBars.Count == 0 || _cells.Count == 0)
        {
            return;
        }

        foreach (var placed in _bannerBars)
        {
            var row = placed.WeekRow;
            var startIndex = (row * 7) + placed.Banner.StartColumn;
            var endIndex = (row * 7) + placed.Banner.EndColumn;

            if (startIndex < 0 || startIndex >= _cells.Count)
            {
                continue;
            }

            endIndex = Math.Min(Math.Max(endIndex, startIndex), _cells.Count - 1);

            var startCell = _cells[startIndex];
            var endCell = _cells[endIndex];

            if (startCell.ActualWidth <= 0 || endCell.ActualWidth <= 0)
            {
                continue;
            }

            var start = startCell.TransformToVisual(placed.Canvas).TransformPoint(new Windows.Foundation.Point(0, 0));
            var end = endCell.TransformToVisual(placed.Canvas).TransformPoint(
                new Windows.Foundation.Point(endCell.ActualWidth, 0));

            var left = start.X + (placed.Banner.ContinuesFromPrev ? 0 : 4);
            var right = end.X - (placed.Banner.ContinuesToNext ? 0 : 4);

            Canvas.SetLeft(placed.Bar, left);
            Canvas.SetTop(placed.Bar, BannerLineTop + (placed.Banner.Lane * MonthDayCell.LineHeight));
            placed.Bar.Width = Math.Max(0, right - left);
        }
    }

    private Border CreateBannerBar(MonthBanner banner)
    {
        var chipBrushes = EventColorService.GetChipBrushes(banner.Color, ActualTheme);

        var content = new Grid();
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(3) });
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var stripe = new Border { Background = chipBrushes.Stripe };
        Grid.SetColumn(stripe, 0);
        content.Children.Add(stripe);

        var text = new TextBlock
        {
            Text = banner.Title,
            FontSize = Tokens.Double("FontSizeChip", 11d),
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = chipBrushes.Text,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(6, 0, 0, 0),
        };

        Grid.SetColumn(text, 1);
        content.Children.Add(text);

        return new Border
        {
            Child = content,
            Background = chipBrushes.Fill,
            Height = MonthDayCell.LineHeight - 1,
            VerticalAlignment = VerticalAlignment.Top,
            CornerRadius = new CornerRadius(
                banner.ContinuesFromPrev ? 0 : BannerCornerRadius,
                banner.ContinuesToNext ? 0 : BannerCornerRadius,
                banner.ContinuesToNext ? 0 : BannerCornerRadius,
                banner.ContinuesFromPrev ? 0 : BannerCornerRadius),
        };
    }

    private void RemoveBannerLayer()
    {
        _bannerBars.Clear();

        foreach (var layer in _bannerLayers)
        {
            MonthHost.Children.Remove(layer);
        }

        _bannerLayers.Clear();
    }

    private sealed record PlacedBanner(Border Bar, MonthBanner Banner, int WeekRow, Canvas Canvas);

    private void EnsureCellCount(int count)
    {
        while (_cells.Count > count)
        {
            var last = _cells[^1];
            MonthHost.Children.Remove(last);
            last.DaySelected -= OnDaySelected;
            last.DayOpened -= OnDayOpened;
            last.NewEventRequested -= OnNewEventRequested;
            last.NewReminderRequested -= OnNewReminderRequested;
            last.ChipDropped -= OnChipDropped;
            last.ChipActivated -= OnChipActivated;
            _cells.RemoveAt(_cells.Count - 1);
        }

        while (_cells.Count < count)
        {
            var cell = new MonthDayCell();

            cell.DaySelected += OnDaySelected;
            cell.DayOpened += OnDayOpened;
            cell.NewEventRequested += OnNewEventRequested;
            cell.NewReminderRequested += OnNewReminderRequested;
            cell.ChipDropped += OnChipDropped;
            cell.ChipActivated += OnChipActivated;

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

            var label = new Border
            {
                BorderBrush = ThemeBrushes.Get(this, "GridLine"),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Child = new TextBlock
                {
                    Text = name.ToUpper(culture),
                    FontSize = Tokens.Double("FontSizeSectionHeader", 11d),
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    CharacterSpacing = 40,
                    Foreground = ThemeBrushes.Get(this, "App.TextSecondaryBrush"),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 8, 0, 8),
                },
            };

            Grid.SetColumn(label, firstColumn + index);
            Grid.SetRow(label, 0);
            WeekdayHeaderHost.Children.Add(label);
        }
    }

    private void OnDaySelected(object? sender, DateOnly date) => ViewModel.SelectDay(date);

    private void OnDayOpened(object? sender, DateOnly date)
    {
        ViewModel.State.SelectDateCommand.Execute(date);
        ViewModel.State.SetViewModeCommand.Execute(CalendarViewMode.Day);
    }

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

    private async void OnChipActivated(object? sender, MonthChipActivation activation)
    {
        var chip = activation.Chip;

        await _details.ShowAsync(activation.Anchor, new CalendarItem
        {
            Id = chip.Id,
            Title = chip.Title,
            StartUtc = chip.StartUtc,
            EndUtc = chip.EndUtc,
            Kind = chip.IsReminder
                ? CalendarItemKind.Reminder
                : chip.IsTask ? CalendarItemKind.Task : CalendarItemKind.Event,
            Color = chip.Color,
            CalendarName = chip.CalendarName,
            Location = chip.Location,
            AllDay = chip.AllDay,
            HasTime = chip.HasTime,
        }, ViewModel.RefreshAsync);
    }
}
