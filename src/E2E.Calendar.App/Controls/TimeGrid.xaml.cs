using System.Collections.Specialized;
using E2E.Calendar.App.Utilities;
using E2E.Calendar.Core;
using E2E.Calendar.Core.Models;
using E2E.Calendar.Core.Services;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.Foundation;
using Windows.System;

namespace E2E.Calendar.App.Controls;

public sealed partial class TimeGrid : UserControl
{
    public static readonly DependencyProperty DaysProperty = DependencyProperty.Register(
        nameof(Days),
        typeof(IReadOnlyList<TimeGridDay>),
        typeof(TimeGrid),
        new PropertyMetadata(null, OnLayoutSourceChanged));

    public static readonly DependencyProperty ItemsProperty = DependencyProperty.Register(
        nameof(Items),
        typeof(IReadOnlyList<CalendarItem>),
        typeof(TimeGrid),
        new PropertyMetadata(null, OnLayoutSourceChanged));

    public static readonly DependencyProperty HourHeightProperty = DependencyProperty.Register(
        nameof(HourHeight),
        typeof(double),
        typeof(TimeGrid),
        new PropertyMetadata(Tokens.Double("GridHourHeight", 56d), OnLayoutSourceChanged));

    public static readonly DependencyProperty NowProperty = DependencyProperty.Register(
        nameof(Now),
        typeof(DateTimeOffset?),
        typeof(TimeGrid),
        new PropertyMetadata(null, OnNowChanged));

    public static readonly DependencyProperty ClockFormatProperty = DependencyProperty.Register(
        nameof(ClockFormat),
        typeof(ClockFormat),
        typeof(TimeGrid),
        new PropertyMetadata(ClockFormat.TwentyFourHour, OnLayoutSourceChanged));

    public static readonly DependencyProperty ShowSecondaryTimeZoneProperty = DependencyProperty.Register(
        nameof(ShowSecondaryTimeZone),
        typeof(bool),
        typeof(TimeGrid),
        new PropertyMetadata(false, OnLayoutSourceChanged));

    public static readonly DependencyProperty SecondaryTimeZoneIdProperty = DependencyProperty.Register(
        nameof(SecondaryTimeZoneId),
        typeof(string),
        typeof(TimeGrid),
        new PropertyMetadata(null, OnLayoutSourceChanged));

    public static readonly DependencyProperty SelectedItemIdProperty = DependencyProperty.Register(
        nameof(SelectedItemId),
        typeof(string),
        typeof(TimeGrid),
        new PropertyMetadata(null, OnLayoutSourceChanged));

    private const int HoursPerDay = 24;
    private const int MaxAllDayRows = 3;
    private const int MaxOverlapColumns = 4;
    private const double ResizeEdgeInPixels = 6;
    private const double SecondaryColumnWidth = 72;
    private const int SnapMinutes = 15;
    private const int FineSnapMinutes = 5;

    private static double _sessionScrollOffset = -1;

    private readonly List<Canvas> _dayCanvases = new();
    private readonly List<FrameworkElement> _headerCells = new();
    private readonly List<FrameworkElement> _allDayCells = new();
    private readonly DispatcherTimer _nowTimer;

    private Canvas? _linesCanvas;
    private Canvas? _gutterCanvas;
    private Canvas? _overlayCanvas;
    private Border? _ghost;
    private TextBlock? _ghostLabel;
    private Ellipse? _nowDot;
    private Rectangle? _nowLine;

    private DragState? _drag;
    private double _columnWidth;
    private int _dayCount;
    private DateTimeOffset _now = DateTimeOffset.Now;

    private bool _rebuildQueued;
    private int _columnsSignature = int.MinValue;
    private double _layersHourHeight = -1;
    private string? _headerSignature;
    private string? _allDaySignature;
    private string? _bodySignature;
    private string? _linesSignature;

    public TimeGrid()
    {
        InitializeComponent();

        _nowTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
        _nowTimer.Tick += OnNowTimerTick;

        SizeChanged += OnSizeChanged;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        IsTabStop = true;
    }

    public IReadOnlyList<TimeGridDay>? Days
    {
        get => (IReadOnlyList<TimeGridDay>?)GetValue(DaysProperty);
        set => SetValue(DaysProperty, value);
    }

    public IReadOnlyList<CalendarItem>? Items
    {
        get => (IReadOnlyList<CalendarItem>?)GetValue(ItemsProperty);
        set => SetValue(ItemsProperty, value);
    }

    public double HourHeight
    {
        get => (double)GetValue(HourHeightProperty);
        set => SetValue(HourHeightProperty, value);
    }

    public DateTimeOffset? Now
    {
        get => (DateTimeOffset?)GetValue(NowProperty);
        set => SetValue(NowProperty, value);
    }

    public ClockFormat ClockFormat
    {
        get => (ClockFormat)GetValue(ClockFormatProperty);
        set => SetValue(ClockFormatProperty, value);
    }

    public bool ShowSecondaryTimeZone
    {
        get => (bool)GetValue(ShowSecondaryTimeZoneProperty);
        set => SetValue(ShowSecondaryTimeZoneProperty, value);
    }

    public string? SecondaryTimeZoneId
    {
        get => (string?)GetValue(SecondaryTimeZoneIdProperty);
        set => SetValue(SecondaryTimeZoneIdProperty, value);
    }

    public string? SelectedItemId
    {
        get => (string?)GetValue(SelectedItemIdProperty);
        set => SetValue(SelectedItemIdProperty, value);
    }

    public event EventHandler<TimeGridItemEventArgs>? ItemActivated;

    public event EventHandler<TimeGridSlotEventArgs>? SlotCreated;

    public event EventHandler<TimeGridItemEventArgs>? ItemMoved;

    public event EventHandler<TimeGridItemEventArgs>? ItemResized;

    public event EventHandler<TimeGridItemEventArgs>? ItemCopied;

    public event EventHandler<TimeGridItemEventArgs>? ItemCompleted;

    public event EventHandler<TimeGridItemActionEventArgs>? ItemActionRequested;

    public event EventHandler<TimeGridSlotActionEventArgs>? SlotActionRequested;

    private static TimeZoneInfo LocalTimeZone => TimeZoneInfo.Local;

    private double GutterWidth => Tokens.Double("GridGutterWidth", 56d);

    private static void OnLayoutSourceChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        if (sender is not TimeGrid grid)
        {
            return;
        }

        if (args.OldValue is INotifyCollectionChanged oldCollection)
        {
            oldCollection.CollectionChanged -= grid.OnItemsCollectionChanged;
        }

        if (args.NewValue is INotifyCollectionChanged newCollection)
        {
            newCollection.CollectionChanged += grid.OnItemsCollectionChanged;
        }

        grid.RequestRebuild();
    }

    private static void OnNowChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        if (sender is not TimeGrid grid)
        {
            return;
        }

        grid._now = args.NewValue is DateTimeOffset value ? value : DateTimeOffset.Now;
        grid.RenderNowLine();
    }

    /// <summary>
    /// Layout work is coalesced to one rebuild per dispatcher turn so a burst of
    /// property changes or resize notifications only measures and renders once.
    /// </summary>
    private void RequestRebuild()
    {
        if (_rebuildQueued)
        {
            return;
        }

        _rebuildQueued = true;

        if (!DispatcherQueue.TryEnqueue(() =>
            {
                _rebuildQueued = false;
                Rebuild();
            }))
        {
            _rebuildQueued = false;
            Rebuild();
        }
    }

    private void OnItemsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RequestRebuild();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _now = Now ?? DateTimeOffset.Now;
        _nowTimer.Start();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _nowTimer.Stop();
    }

    private void OnNowTimerTick(object? sender, object e)
    {
        _now = DateTimeOffset.Now;
        RenderNowLine();
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs args)
    {
        RequestRebuild();
    }

    private void OnViewChanged(object sender, ScrollViewerViewChangedEventArgs args)
    {
        if (args.IsIntermediate)
        {
            return;
        }

        _sessionScrollOffset = BodyScroll.VerticalOffset;
        UpdateScrollIndicator();
        RenderBody();
    }

    private void Rebuild()
    {
        var days = Days ?? Array.Empty<TimeGridDay>();
        _dayCount = days.Count;

        if (days.Count == 0)
        {
            return;
        }

        ApplySharedColumns(days.Count);

        if (_dayCanvases.Count != days.Count || Math.Abs(_layersHourHeight - HourHeight) > 0.01)
        {
            CreateBodyLayers(days.Count);
            _layersHourHeight = HourHeight;
            _bodySignature = null;
            _linesSignature = null;
        }

        _columnWidth = ComputeColumnWidth(days.Count);

        RenderHeader(days);
        RenderAllDayStrip();
        RenderBody();
        RenderNowLine();
        UpdateEmptyHint();
        UpdateScrollIndicator();
        EnsureInitialScroll(days);
    }

    private static string DaySignature(IReadOnlyList<TimeGridDay> days)
    {
        var builder = new System.Text.StringBuilder(days.Count * 12);

        foreach (var day in days)
        {
            builder
                .Append(day.Date.DayNumber)
                .Append(day.IsToday ? 'T' : 'n')
                .Append(day.IsWeekend ? 'W' : 'w');
        }

        return builder.ToString();
    }

    private string ItemSignature()
    {
        var identity = Items is null ? 0 : System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(Items);

        return $"{identity}:{Items?.Count ?? 0}";
    }

    private void ApplySharedColumns(int dayCount)
    {
        var total = dayCount + 1 + (ShowSecondaryTimeZone ? 1 : 0);
        var signature = (dayCount * 2) + (ShowSecondaryTimeZone ? 1 : 0);

        if (_columnsSignature == signature)
        {
            Grid.SetColumnSpan(BodyScroll, total);
            Grid.SetColumnSpan(EmptyHint, total);
            Grid.SetColumn(ScrollIndicator, total - 1);
            return;
        }

        _columnsSignature = signature;

        foreach (var grid in new[] { RootLayout, BodyGrid })
        {
            grid.ColumnDefinitions.Clear();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(GutterWidth) });

            for (var index = 0; index < dayCount; index++)
            {
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            }

            if (ShowSecondaryTimeZone)
            {
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(SecondaryColumnWidth) });
            }
        }

        Grid.SetColumnSpan(BodyScroll, total);
        Grid.SetColumnSpan(EmptyHint, total);
        Grid.SetColumn(ScrollIndicator, total - 1);
    }

    private void CreateBodyLayers(int dayCount)
    {
        var total = dayCount + 1 + (ShowSecondaryTimeZone ? 1 : 0);

        BodyGrid.Children.Clear();
        BodyGrid.Height = HourHeight * HoursPerDay;

        _linesCanvas = new Canvas { IsHitTestVisible = false };
        Grid.SetColumn(_linesCanvas, 0);
        Grid.SetColumnSpan(_linesCanvas, total);
        BodyGrid.Children.Add(_linesCanvas);

        _dayCanvases.Clear();

        for (var index = 0; index < dayCount; index++)
        {
            var canvas = new Canvas
            {
                Height = HourHeight * HoursPerDay,
                Width = ComputeColumnWidth(dayCount),
            };

            Grid.SetColumn(canvas, index + 1);
            canvas.PointerPressed += OnDayCanvasPointerPressed;
            canvas.PointerMoved += OnDayCanvasPointerMoved;
            canvas.PointerReleased += OnDayCanvasPointerReleased;
            canvas.PointerCaptureLost += OnDayCanvasPointerReleased;
            canvas.RightTapped += OnDayCanvasRightTapped;
            BodyGrid.Children.Add(canvas);
            _dayCanvases.Add(canvas);
        }

        _gutterCanvas = new Canvas();
        Grid.SetColumn(_gutterCanvas, 0);
        BodyGrid.Children.Add(_gutterCanvas);

        _overlayCanvas = new Canvas { IsHitTestVisible = false };
        Grid.SetColumn(_overlayCanvas, 0);
        Grid.SetColumnSpan(_overlayCanvas, total);
        BodyGrid.Children.Add(_overlayCanvas);

        _nowLine = new Rectangle
        {
            Height = 2,
            Fill = (Brush)Application.Current.Resources["SystemFillColorCriticalBrush"],
            Visibility = Visibility.Collapsed,
        };

        _overlayCanvas.Children.Add(_nowLine);

        _nowDot = new Ellipse
        {
            Width = 8,
            Height = 8,
            Fill = (Brush)Application.Current.Resources["SystemFillColorCriticalBrush"],
            Visibility = Visibility.Collapsed,
        };

        _overlayCanvas.Children.Add(_nowDot);

        _ghost = new Border
        {
            Background = (Brush)Application.Current.Resources["AccentFillColorDefaultBrush"],
            BorderBrush = (Brush)Application.Current.Resources["AccentControlElevationBorderBrush"],
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(Tokens.Double("RadiusChip", 6d)),
            Opacity = 0.35,
            Visibility = Visibility.Collapsed,
        };

        _overlayCanvas.Children.Add(_ghost);

        _ghostLabel = new TextBlock
        {
            FontSize = 11,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = (Brush)Application.Current.Resources["TextFillColorPrimaryBrush"],
            Visibility = Visibility.Collapsed,
        };

        _overlayCanvas.Children.Add(_ghostLabel);
    }

    private double ComputeColumnWidth(int dayCount)
    {
        // The viewport width is independent of the content, so the columns never
        // feed back into the size of the grid they are measured against.
        var width = BodyScroll.ViewportWidth > 0 ? BodyScroll.ViewportWidth : RootLayout.ActualWidth;

        if (width <= 0)
        {
            width = ActualWidth;
        }

        if (width <= 0)
        {
            return 120;
        }

        var reserved = GutterWidth + (ShowSecondaryTimeZone ? SecondaryColumnWidth : 0);

        return Math.Max(40, (width - reserved) / dayCount);
    }

    private void RenderHeader(IReadOnlyList<TimeGridDay> days)
    {
        var signature = DaySignature(days);

        if (_headerCells.Count > 0 && _headerSignature == signature)
        {
            return;
        }

        _headerSignature = signature;

        foreach (var cell in _headerCells)
        {
            RootLayout.Children.Remove(cell);
        }

        _headerCells.Clear();

        var tzLabel = new TextBlock
        {
            Text = FormatTimeZoneOffset(LocalTimeZone),
            FontSize = 11,
            Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(0, 0, 8, 6),
        };

        Grid.SetRow(tzLabel, 0);
        Grid.SetColumn(tzLabel, 0);
        RootLayout.Children.Add(tzLabel);
        _headerCells.Add(tzLabel);

        for (var index = 0; index < days.Count; index++)
        {
            var day = days[index];
            var cell = CreateHeaderCell(day);
            Grid.SetRow(cell, 0);
            Grid.SetColumn(cell, index + 1);
            RootLayout.Children.Add(cell);
            _headerCells.Add(cell);
        }
    }

    private Border CreateHeaderCell(TimeGridDay day)
    {
        var content = new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Spacing = 2,
        };

        content.Children.Add(new TextBlock
        {
            Text = day.WeekdayLabel.ToUpperInvariant(),
            FontSize = 12,
            Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
            HorizontalAlignment = HorizontalAlignment.Center,
        });

        var dateHost = new Grid
        {
            Width = 32,
            Height = 32,
            HorizontalAlignment = HorizontalAlignment.Center,
        };

        var dateText = new TextBlock
        {
            Text = day.Date.Day.ToString(System.Globalization.CultureInfo.CurrentCulture),
            FontSize = 15,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };

        if (day.IsToday)
        {
            dateHost.Background = (Brush)Application.Current.Resources["AccentFillColorDefaultBrush"];
            dateHost.CornerRadius = new CornerRadius(16);
            dateText.Foreground = (Brush)Application.Current.Resources["TextOnAccentFillColorPrimaryBrush"];
        }
        else
        {
            dateText.Foreground = (Brush)Application.Current.Resources["TextFillColorPrimaryBrush"];
        }

        dateHost.Children.Add(dateText);
        content.Children.Add(dateHost);

        Brush? background = null;

        if (day.IsToday)
        {
            background = ColorHelper.FromColor(Windows.UI.Color.FromArgb(0x0F, 0x00, 0x78, 0xD4));
        }
        else if (day.IsWeekend)
        {
            background = ColorHelper.FromColor(Windows.UI.Color.FromArgb(0x08, 0x00, 0x00, 0x00));
        }

        return new Border
        {
            Child = content,
            Background = background,
            MinHeight = Tokens.Double("GridDayHeaderHeight", 60d),
            Padding = new Thickness(0, 4, 0, 4),
        };
    }

    private void RenderAllDayStrip()
    {
        var days = Days ?? Array.Empty<TimeGridDay>();

        if (days.Count == 0)
        {
            return;
        }

        var signature = $"{DaySignature(days)}|{ItemSignature()}";

        if (_allDayCells.Count > 0 && _allDaySignature == signature)
        {
            return;
        }

        _allDaySignature = signature;

        foreach (var cell in _allDayCells)
        {
            RootLayout.Children.Remove(cell);
        }

        _allDayCells.Clear();

        var label = new TextBlock
        {
            Text = "all-day",
            FontSize = 11,
            Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 6, 8, 0),
        };

        Grid.SetRow(label, 1);
        Grid.SetColumn(label, 0);
        RootLayout.Children.Add(label);
        _allDayCells.Add(label);

        var allDayItems = (Items ?? Array.Empty<CalendarItem>())
            .Where(item => item.AllDay)
            .OrderBy(item => item.StartUtc)
            .ToList();

        var rowEnds = new List<int>();
        var overflow = new Dictionary<int, int>();

        foreach (var item in allDayItems)
        {
            var startIndex = DayIndexFor(item.StartUtc, days);
            var endIndex = DayIndexFor(item.EndUtc.AddTicks(-1), days);

            if (startIndex < 0)
            {
                continue;
            }

            if (endIndex < startIndex)
            {
                endIndex = startIndex;
            }

            var row = rowEnds.FindIndex(end => end < startIndex);

            if (row < 0)
            {
                row = rowEnds.Count;
                rowEnds.Add(endIndex);
            }
            else
            {
                rowEnds[row] = endIndex;
            }

            if (row >= MaxAllDayRows)
            {
                overflow[endIndex] = overflow.TryGetValue(endIndex, out var count) ? count + 1 : 1;
                continue;
            }

            var bar = CreateAllDayBar(item);
            Grid.SetRow(bar, 1 + row);
            Grid.SetColumn(bar, startIndex + 1);
            Grid.SetColumnSpan(bar, endIndex - startIndex + 1);
            RootLayout.Children.Add(bar);
            _allDayCells.Add(bar);
        }

        foreach (var pair in overflow)
        {
            var chip = new TextBlock
            {
                Text = $"+{pair.Value} more",
                FontSize = 11,
                Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(4, 2, 4, 2),
            };

            Grid.SetRow(chip, MaxAllDayRows);
            Grid.SetColumn(chip, pair.Key + 1);
            RootLayout.Children.Add(chip);
            _allDayCells.Add(chip);
        }
    }

    private Border CreateAllDayBar(CalendarItem item)
    {
        var color = ColorHelper.TryParse(item.Color, out var parsed)
            ? parsed
            : Windows.UI.Color.FromArgb(0xFF, 0x00, 0x78, 0xD4);

        var content = new Grid();
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(3) });
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var bar = new Border { Background = ColorHelper.FromColor(color) };
        Grid.SetColumn(bar, 0);
        content.Children.Add(bar);

        var text = new TextBlock
        {
            Text = item.Title,
            FontSize = 12,
            Foreground = (Brush)Application.Current.Resources["TextFillColorPrimaryBrush"],
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(6, 3, 4, 3),
        };

        Grid.SetColumn(text, 1);
        content.Children.Add(text);

        return new Border
        {
            Child = content,
            Background = ColorHelper.FromColor(Windows.UI.Color.FromArgb(0x2E, color.R, color.G, color.B)),
            CornerRadius = new CornerRadius(Tokens.Double("RadiusChip", 6d)),
            Margin = new Thickness(2, 1, 2, 1),
        };
    }

    private void RenderBody()
    {
        var days = Days ?? Array.Empty<TimeGridDay>();

        if (days.Count == 0 || _dayCanvases.Count == 0)
        {
            return;
        }

        _columnWidth = ComputeColumnWidth(days.Count);

        var visible = VisibleRange();
        var signature = string.Join(
            '|',
            DaySignature(days),
            ItemSignature(),
            SelectedItemId,
            ClockFormat,
            Math.Round(_columnWidth, 1),
            Math.Round(HourHeight, 1),
            Math.Round((visible.Start - DateTimeOffset.UnixEpoch).TotalMinutes / 10),
            Math.Round((visible.End - visible.Start).TotalMinutes, 1));

        if (_bodySignature == signature)
        {
            return;
        }

        _bodySignature = signature;

        foreach (var canvas in _dayCanvases)
        {
            canvas.Children.Clear();
            canvas.Width = _columnWidth;
        }

        var timed = (Items ?? Array.Empty<CalendarItem>())
            .Where(item => !item.AllDay)
            .Where(item => item.EndUtc > visible.Start && item.StartUtc < visible.End)
            .ToList();

        var reminders = (Items ?? Array.Empty<CalendarItem>())
            .Where(item => item.Kind is CalendarItemKind.Reminder or CalendarItemKind.Task)
            .Where(item => item.StartUtc >= visible.Start && item.StartUtc < visible.End)
            .ToList();

        for (var dayIndex = 0; dayIndex < days.Count; dayIndex++)
        {
            var dayStart = DayStartUtc(days[dayIndex].Date);
            var dayEnd = dayStart.AddDays(1);

            var dayItems = timed
                .Where(item => item.StartUtc < dayEnd && item.EndUtc > dayStart)
                .ToList();

            var packed = OverlapLayout.Pack(dayItems, dayStart, dayEnd);

            foreach (var group in packed.GroupBy(entry => entry.ClusterIndex))
            {
                var entries = group.ToList();
                var columnCount = Math.Min(entries[0].ColumnCount, MaxOverlapColumns);
                var hidden = entries.Count(entry => entry.Column >= MaxOverlapColumns);

                foreach (var entry in entries)
                {
                    if (entry.Column >= MaxOverlapColumns)
                    {
                        continue;
                    }

                    var chip = CreateChip(entry.Item, days[dayIndex]);

                    var y = (entry.Item.StartUtc - dayStart).TotalHours * HourHeight;
                    var height = Math.Max(22, (entry.Item.EndUtc - entry.Item.StartUtc).TotalHours * HourHeight - 2);
                    var width = Math.Max(24, (_columnWidth / columnCount) - 1);

                    Canvas.SetTop(chip, y);
                    Canvas.SetLeft(chip, (entry.Column * _columnWidth / columnCount) + 1);
                    chip.Width = width;
                    chip.Height = height;

                    _dayCanvases[dayIndex].Children.Add(chip);
                }

                if (hidden > 0)
                {
                    var more = new TextBlock
                    {
                        Text = $"+{hidden}",
                        FontSize = 11,
                        Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
                        HorizontalAlignment = HorizontalAlignment.Left,
                    };

                    Canvas.SetLeft(more, ((MaxOverlapColumns - 1) * _columnWidth / columnCount) + 6);
                    Canvas.SetTop(more, Math.Max(0, (dayEnd - dayStart).TotalHours * HourHeight - 22));
                    _dayCanvases[dayIndex].Children.Add(more);
                }
            }

            foreach (var reminder in reminders.Where(item => item.StartUtc >= dayStart && item.StartUtc < dayEnd))
            {
                var chip = CreateChip(reminder, days[dayIndex]);

                var y = (reminder.StartUtc - dayStart).TotalHours * HourHeight;

                Canvas.SetTop(chip, y);
                Canvas.SetLeft(chip, 4);
                chip.Width = Math.Max(24, _columnWidth - 8);
                chip.Height = 22;

                _dayCanvases[dayIndex].Children.Add(chip);
            }
        }

        RenderLines();
        RenderNowLine();
    }

    private void RenderLines()
    {
        if (_linesCanvas is null || _gutterCanvas is null)
        {
            return;
        }

        var width = BodyGrid.ActualWidth > 0 ? BodyGrid.ActualWidth : RootLayout.ActualWidth;
        var signature = string.Join(
            '|',
            Math.Round(width, 1),
            Math.Round(HourHeight, 1),
            Days?.Count ?? 0,
            ShowSecondaryTimeZone,
            SecondaryTimeZoneId,
            ClockFormat);

        if (_linesSignature == signature)
        {
            return;
        }

        _linesSignature = signature;

        _linesCanvas.Children.Clear();
        _gutterCanvas.Children.Clear();

        var divider = (Brush)Application.Current.Resources["DividerStrokeColorDefaultBrush"];

        for (var hour = 0; hour <= HoursPerDay; hour++)
        {
            var y = hour * HourHeight;

            var line = new Rectangle
            {
                Height = 1,
                Width = Math.Max(0, width),
                Fill = divider,
            };

            Canvas.SetTop(line, y);
            _linesCanvas.Children.Add(line);

            if (hour == HoursPerDay)
            {
                continue;
            }

            var half = new Rectangle
            {
                Height = 1,
                Width = Math.Max(0, width),
                Fill = divider,
                Opacity = 0.4,
            };

            Canvas.SetTop(half, y + (HourHeight / 2));
            _linesCanvas.Children.Add(half);

            if (hour == 0)
            {
                continue;
            }

            var label = new TextBlock
            {
                Text = FormatHour(hour),
                FontSize = 12,
                Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
            };

            Canvas.SetTop(label, y - 8);
            Canvas.SetLeft(label, GutterWidth - 54);
            label.Width = 46;
            _gutterCanvas.Children.Add(label);
        }

        var days = Days ?? Array.Empty<TimeGridDay>();

        for (var index = 1; index < days.Count; index++)
        {
            var separator = new Rectangle
            {
                Width = 1,
                Height = HourHeight * HoursPerDay,
                Fill = divider,
                Opacity = 0.6,
            };

            Canvas.SetLeft(separator, GutterWidth + (index * _columnWidth));
            _linesCanvas.Children.Add(separator);
        }

        if (ShowSecondaryTimeZone)
        {
            RenderSecondaryLabels(days, width);
        }
    }

    private void RenderSecondaryLabels(IReadOnlyList<TimeGridDay> days, double width)
    {
        if (_gutterCanvas is null)
        {
            return;
        }

        var secondary = ResolveSecondaryTimeZone();
        var left = GutterWidth + (days.Count * _columnWidth);

        for (var hour = 1; hour < HoursPerDay; hour++)
        {
            var local = secondary.ToLocal(DayStartUtc(days[0].Date).AddHours(hour));

            var label = new TextBlock
            {
                Text = FormatLocalTime(local),
                FontSize = 12,
                Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
            };

            Canvas.SetTop(label, (hour * HourHeight) - 8);
            Canvas.SetLeft(label, left + 8);
            _gutterCanvas.Children.Add(label);
        }
    }

    private void RenderNowLine()
    {
        if (_nowLine is null || _nowDot is null)
        {
            return;
        }

        var days = Days ?? Array.Empty<TimeGridDay>();
        var now = _now;
        var today = DateOnly.FromDateTime(LocalTimeZone.ToLocal(now));
        var todayIndex = days.ToList().FindIndex(day => day.Date == today);

        if (todayIndex < 0 || todayIndex >= _dayCanvases.Count)
        {
            _nowLine.Visibility = Visibility.Collapsed;
            _nowDot.Visibility = Visibility.Collapsed;
            return;
        }

        var dayStart = DayStartUtc(today);
        var y = (now - dayStart).TotalHours * HourHeight;
        var left = GutterWidth + (todayIndex * _columnWidth);

        _nowLine.Width = Math.Max(0, _columnWidth);
        _nowLine.Visibility = Visibility.Visible;
        Canvas.SetTop(_nowLine, y);
        Canvas.SetLeft(_nowLine, left);

        _nowDot.Visibility = Visibility.Visible;
        Canvas.SetTop(_nowDot, y - 3);
        Canvas.SetLeft(_nowDot, left - 4);
    }

    private void UpdateEmptyHint()
    {
        var days = Days ?? Array.Empty<TimeGridDay>();

        if (days.Count == 0)
        {
            EmptyHint.Visibility = Visibility.Collapsed;
            return;
        }

        var visible = VisibleRange();
        var hasContent = (Items ?? Array.Empty<CalendarItem>())
            .Any(item => item.StartUtc < visible.End && item.EndUtc > visible.Start);

        EmptyHint.Visibility = hasContent ? Visibility.Collapsed : Visibility.Visible;
    }

    private void UpdateScrollIndicator()
    {
        var scrollable = BodyScroll.ScrollableHeight;

        if (scrollable <= 1)
        {
            ScrollIndicator.Visibility = Visibility.Collapsed;
            return;
        }

        var viewport = BodyScroll.ViewportHeight;
        var ratio = viewport / (viewport + scrollable);
        var height = Math.Max(24, BodyGrid.ActualHeight * ratio);
        var travel = Math.Max(1, BodyScroll.ViewportHeight - height);
        var progress = Math.Clamp(BodyScroll.VerticalOffset / scrollable, 0, 1);

        ScrollIndicator.Height = height;
        ScrollIndicator.Margin = new Thickness(0, 2 + (travel * progress), 2, 2);
        ScrollIndicator.Visibility = Visibility.Visible;
    }

    private void EnsureInitialScroll(IReadOnlyList<TimeGridDay> days)
    {
        if (_hasScrolled)
        {
            return;
        }

        _hasScrolled = true;

        if (_sessionScrollOffset >= 0)
        {
            ApplyScroll(_sessionScrollOffset);
            return;
        }

        var now = Now ?? DateTimeOffset.Now;
        var today = DateOnly.FromDateTime(LocalTimeZone.ToLocal(now));
        var isTodayVisible = days.Any(day => day.Date == today);


        var offset = isTodayVisible
            ? ((now.AddMinutes(-90) - DayStartUtc(today)).TotalMinutes / 60 * HourHeight)
            : 7 * HourHeight;

        ApplyScroll(Math.Max(0, offset));
    }

    private void ApplyScroll(double offset)
    {

        if (BodyScroll.ScrollableHeight <= 0)
        {
            _pendingScrollOffset = offset;
            return;
        }

        _pendingScrollOffset = -1;
        BodyScroll.ChangeView(null, offset, null, true);
    }

    private void OnBodyScrollSizeChanged(object sender, SizeChangedEventArgs args)
    {
        if (_pendingScrollOffset >= 0)
        {
            ApplyScroll(_pendingScrollOffset);
        }
    }

    private bool _hasScrolled;
    private double _pendingScrollOffset = -1;

    private EventChipView CreateChip(CalendarItem item, TimeGridDay day)
    {
        var isTickable = item.IsTickable;
        var isShort = (item.EndUtc - item.StartUtc) < TimeSpan.FromMinutes(30);

        var chip = new EventChipView
        {
            Title = item.Title,
            TimeText = isTickable ? string.Empty : FormatTimeRange(item),
            ColorHex = item.Color,
            IsTickable = isTickable,
            IsDone = item.IsDone,
            ShowTime = !isTickable && !isShort,
            IsSelected = string.Equals(item.Id, SelectedItemId, StringComparison.Ordinal),
            TooltipText = BuildTooltip(item),
        };

        chip.Tag = item;
        chip.PointerPressed += OnChipPointerPressed;
        chip.RightTapped += OnChipRightTapped;
        chip.CheckToggled += OnChipCheckToggled;

        return chip;
    }

    private void OnChipRightTapped(object sender, RightTappedRoutedEventArgs e)
    {
        if (sender is not EventChipView chip || chip.Tag is not CalendarItem item)
        {
            return;
        }

        var flyout = BuildContextMenu(item);

        flyout.ShowAt(chip, new FlyoutShowOptions { Position = e.GetPosition(chip) });
        e.Handled = true;
    }

    private string BuildTooltip(CalendarItem item)
    {
        var parts = new List<string> { item.Title };

        if (!item.AllDay)
        {
            parts.Add($"{FormatTime(item.StartUtc)} to {FormatTime(item.EndUtc)}");
        }
        else
        {
            parts.Add("All day");
        }

        if (!string.IsNullOrWhiteSpace(item.Location))
        {
            parts.Add(item.Location);
        }

        if (!string.IsNullOrWhiteSpace(item.CalendarName))
        {
            parts.Add(item.CalendarName);
        }

        return string.Join(", ", parts);
    }

    private MenuFlyout BuildContextMenu(CalendarItem item)
    {
        var flyout = new MenuFlyout();

        var edit = new MenuFlyoutItem { Text = "Edit", Icon = new SymbolIcon(Symbol.Edit) };
        edit.Click += (_, _) => ItemActionRequested?.Invoke(this, new TimeGridItemActionEventArgs(item, TimeGridItemAction.Edit));
        flyout.Items.Add(edit);

        var duplicate = new MenuFlyoutItem { Text = "Duplicate", Icon = new SymbolIcon(Symbol.Copy) };
        duplicate.Click += (_, _) => ItemActionRequested?.Invoke(this, new TimeGridItemActionEventArgs(item, TimeGridItemAction.Duplicate));
        flyout.Items.Add(duplicate);

        var colour = new MenuFlyoutSubItem { Text = "Change colour" };

        foreach (var hex in CalendarPalette.All)
        {
            var entry = new MenuFlyoutItem { Text = hex };
            entry.Click += (_, _) => ItemActionRequested?.Invoke(this, new TimeGridItemActionEventArgs(item with { Color = hex }, TimeGridItemAction.ChangeColor));
            colour.Items.Add(entry);
        }

        flyout.Items.Add(colour);

        var delete = new MenuFlyoutItem { Text = "Delete", Icon = new SymbolIcon(Symbol.Delete) };
        delete.Click += (_, _) => ItemActionRequested?.Invoke(this, new TimeGridItemActionEventArgs(item, TimeGridItemAction.Delete));
        flyout.Items.Add(delete);

        return flyout;
    }

    private void OnChipCheckToggled(object? sender, EventArgs e)
    {
        if (sender is EventChipView chip && chip.Tag is CalendarItem item)
        {
            ItemCompleted?.Invoke(this, new TimeGridItemEventArgs(item with { IsDone = !item.IsDone }));
        }
    }

    private void OnDayCanvasRightTapped(object sender, RightTappedRoutedEventArgs e)
    {
        if (sender is not Canvas canvas)
        {
            return;
        }

        var dayIndex = _dayCanvases.IndexOf(canvas);
        var days = Days ?? Array.Empty<TimeGridDay>();

        if (dayIndex < 0 || dayIndex >= days.Count)
        {
            return;
        }

        var position = e.GetPosition(canvas);
        var minutes = Math.Round((position.Y / HourHeight) * 60 / 15) * 15;
        var startUtc = DayStartUtc(days[dayIndex].Date).AddMinutes(minutes);

        var flyout = new MenuFlyout();

        var newEvent = new MenuFlyoutItem { Text = "New event here", Icon = new SymbolIcon(Symbol.Add) };
        newEvent.Click += (_, _) => SlotActionRequested?.Invoke(this, new TimeGridSlotActionEventArgs(days[dayIndex].Date, startUtc, TimeGridSlotAction.NewEvent));
        flyout.Items.Add(newEvent);

        var newReminder = new MenuFlyoutItem { Text = "New reminder here" };
        newReminder.Click += (_, _) => SlotActionRequested?.Invoke(this, new TimeGridSlotActionEventArgs(days[dayIndex].Date, startUtc, TimeGridSlotAction.NewReminder));
        flyout.Items.Add(newReminder);

        flyout.ShowAt(canvas, new FlyoutShowOptions { Position = e.GetPosition(canvas) });
    }

    private void OnChipPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (sender is not EventChipView chip || chip.Tag is not CalendarItem item)
        {
            return;
        }

        e.Handled = true;

        ItemActivated?.Invoke(this, new TimeGridItemEventArgs(item));
    }

    private void OnDayCanvasPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (_dayCanvases.Count == 0)
        {
            return;
        }

        var canvas = (Canvas)sender;
        var dayIndex = _dayCanvases.IndexOf(canvas);

        if (dayIndex < 0)
        {
            return;
        }

        Focus(FocusState.Programmatic);

        var position = e.GetCurrentPoint(canvas).Position;
        var hit = HitTest(canvas, position);

        if (hit is not null && hit.Tag is CalendarItem hitItem)
        {
            var mode = IsOnResizeEdge(hit, position) ? DragMode.Resize : DragMode.Move;

            _drag = new DragState
            {
                Mode = mode,
                Item = hitItem,
                DayIndex = dayIndex,
                StartUtc = hitItem.StartUtc,
                EndUtc = hitItem.EndUtc,
                GrabOffset = TimeAt(dayIndex, position.Y) - hitItem.StartUtc,
                IsCopy = mode == DragMode.Move && IsControlDown(),
            };
        }
        else
        {
            var time = TimeAt(dayIndex, position.Y);

            _drag = new DragState
            {
                Mode = DragMode.Create,
                DayIndex = dayIndex,
                StartUtc = time,
                EndUtc = time.AddMinutes(SnapMinutes),
            };
        }

        canvas.CapturePointer(e.Pointer);
        ShowPreview();
    }

    private void OnDayCanvasPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (_drag is null)
        {
            return;
        }

        var canvas = (Canvas)sender;
        var position = e.GetCurrentPoint(canvas).Position;
        var time = TimeAt(_drag.DayIndex, position.Y);

        switch (_drag.Mode)
        {
            case DragMode.Create:
                _drag.StartUtc = time < _drag.EndUtc ? time : _drag.EndUtc;
                _drag.EndUtc = time < _drag.StartUtc ? _drag.StartUtc : time;
                break;

            case DragMode.Move:
                var length = _drag.EndUtc - _drag.StartUtc;
                _drag.StartUtc = time - _drag.GrabOffset;
                _drag.EndUtc = _drag.StartUtc + length;
                break;

            case DragMode.Resize:
                _drag.EndUtc = time < _drag.StartUtc ? _drag.StartUtc : time;
                break;
        }

        ShowPreview();
    }

    private void OnDayCanvasPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (_drag is null)
        {
            return;
        }

        var drag = _drag;
        _drag = null;

        HidePreview();

        switch (drag.Mode)
        {
            case DragMode.Create:
                SlotCreated?.Invoke(this, new TimeGridSlotEventArgs(drag.StartUtc, drag.EndUtc));
                break;

            case DragMode.Move:
                var moved = drag.Item! with { StartUtc = drag.StartUtc, EndUtc = drag.EndUtc };

                if (drag.IsCopy)
                {
                    ItemCopied?.Invoke(this, new TimeGridItemEventArgs(moved));
                }
                else
                {
                    ItemMoved?.Invoke(this, new TimeGridItemEventArgs(moved));
                }

                break;

            case DragMode.Resize:
                ItemResized?.Invoke(this, new TimeGridItemEventArgs(drag.Item! with { EndUtc = drag.EndUtc }));
                break;
        }
    }

    private void OnGridKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != VirtualKey.Escape || _drag is null)
        {
            return;
        }

        _drag = null;
        HidePreview();
        e.Handled = true;
    }

    private void ShowPreview()
    {
        if (_drag is null || _ghost is null || _ghostLabel is null)
        {
            return;
        }

        var days = Days ?? Array.Empty<TimeGridDay>();

        if (_drag.DayIndex >= days.Count)
        {
            return;
        }

        var dayStart = DayStartUtc(days[_drag.DayIndex].Date);
        var top = (_drag.StartUtc - dayStart).TotalHours * HourHeight;
        var height = Math.Max(22, (_drag.EndUtc - _drag.StartUtc).TotalHours * HourHeight);

        _ghost.Visibility = Visibility.Visible;
        _ghost.Width = Math.Max(24, _columnWidth - 4);
        _ghost.Height = height;
        Canvas.SetTop(_ghost, top);
        Canvas.SetLeft(_ghost, GutterWidth + (_drag.DayIndex * _columnWidth) + 2);

        _ghostLabel.Visibility = Visibility.Visible;
        _ghostLabel.Text = $"{FormatTime(_drag.StartUtc)} – {FormatTime(_drag.EndUtc)}";
        Canvas.SetTop(_ghostLabel, top - 16);
        Canvas.SetLeft(_ghostLabel, GutterWidth + (_drag.DayIndex * _columnWidth) + 4);
    }

    private void HidePreview()
    {
        if (_ghost is not null)
        {
            _ghost.Visibility = Visibility.Collapsed;
        }

        if (_ghostLabel is not null)
        {
            _ghostLabel.Visibility = Visibility.Collapsed;
        }
    }

    private EventChipView? HitTest(Canvas canvas, Point position)
    {
        foreach (var child in canvas.Children.OfType<EventChipView>())
        {
            var left = Canvas.GetLeft(child);
            var top = Canvas.GetTop(child);

            if (position.X >= left && position.X <= left + child.Width &&
                position.Y >= top && position.Y <= top + child.Height)
            {
                return child;
            }
        }

        return null;
    }

    private static bool IsOnResizeEdge(EventChipView chip, Point position)
    {
        var top = Canvas.GetTop(chip);
        var left = Canvas.GetLeft(chip);

        var nearStart = Math.Abs(position.Y - top) <= ResizeEdgeInPixels;
        var nearEnd = Math.Abs(position.Y - (top + chip.Height)) <= ResizeEdgeInPixels;

        return (nearStart || nearEnd) && position.X >= left && position.X <= left + chip.Width;
    }

    private DateTimeOffset TimeAt(int dayIndex, double y)
    {
        var days = Days ?? Array.Empty<TimeGridDay>();
        var dayStart = DayStartUtc(days[dayIndex].Date);
        var minutes = y / HourHeight * 60;
        var snap = IsShiftDown() ? FineSnapMinutes : SnapMinutes;
        var snapped = Math.Round(minutes / snap) * snap;

        return dayStart.AddMinutes(snapped);
    }

    private (DateTimeOffset Start, DateTimeOffset End) VisibleRange()
    {
        var offset = BodyScroll.VerticalOffset;
        var viewport = BodyScroll.ViewportHeight > 0 ? BodyScroll.ViewportHeight : ActualHeight;
        var firstDay = Days is { Count: > 0 } ? DayStartUtc(Days[0].Date) : DateTimeOffset.UnixEpoch;
        var start = firstDay.AddMinutes(offset / HourHeight * 60);

        return (start, start.AddMinutes(viewport / HourHeight * 60));
    }

    private DateTimeOffset DayStartUtc(DateOnly date)
    {
        return LocalTimeZone.ToUtc(date.ToDateTime(TimeOnly.MinValue));
    }

    private int DayIndexFor(DateTimeOffset utc, IReadOnlyList<TimeGridDay> days)
    {
        var local = DateOnly.FromDateTime(LocalTimeZone.ToLocal(utc));

        for (var index = 0; index < days.Count; index++)
        {
            if (days[index].Date == local)
            {
                return index;
            }
        }

        return -1;
    }

    private string FormatTime(DateTimeOffset value)
    {
        return FormatLocalTime(LocalTimeZone.ToLocal(value));
    }

    private string FormatLocalTime(DateTime local)
    {
        return ClockFormat == ClockFormat.TwentyFourHour
            ? local.ToString("HH:mm", System.Globalization.CultureInfo.CurrentCulture)
            : local.ToString("h:mm tt", System.Globalization.CultureInfo.CurrentCulture);
    }

    private string FormatTimeRange(CalendarItem item)
    {
        var start = LocalTimeZone.ToLocal(item.StartUtc);
        var end = LocalTimeZone.ToLocal(item.EndUtc);

        if (ClockFormat == ClockFormat.TwentyFourHour)
        {
            return $"{start:HH:mm} – {end:HH:mm}";
        }

        return $"{start:h:mm tt} – {end:h:mm tt}";
    }

    private string FormatHour(int hour)
    {
        if (ClockFormat == ClockFormat.TwentyFourHour)
        {
            return hour.ToString("00", System.Globalization.CultureInfo.CurrentCulture) + ":00";
        }

        var suffix = hour >= 12 ? "PM" : "AM";
        var display = hour % 12 == 0 ? 12 : hour % 12;

        return $"{display} {suffix}";
    }

    private static string FormatTimeZoneOffset(TimeZoneInfo zone)
    {
        var offset = zone.GetUtcOffset(DateTimeOffset.UtcNow);
        var sign = offset < TimeSpan.Zero ? "-" : "+";
        var absolute = offset.Duration();

        return $"{sign}{absolute.Hours:00}:{absolute.Minutes:00}";
    }

    private TimeZoneInfo ResolveSecondaryTimeZone()
    {
        if (!string.IsNullOrWhiteSpace(SecondaryTimeZoneId) &&
            SecondaryTimeZoneId.TryToTimeZoneInfo(out var zone))
        {
            return zone;
        }

        return TimeZoneInfo.Utc;
    }

    private static bool IsControlDown() =>
        (InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control) & Windows.UI.Core.CoreVirtualKeyStates.Down) != 0;

    private static bool IsShiftDown() =>
        (InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Shift) & Windows.UI.Core.CoreVirtualKeyStates.Down) != 0;

    private enum DragMode
    {
        None,
        Create,
        Move,
        Resize,
    }

    private sealed class DragState
    {
        public DragMode Mode { get; init; }

        public CalendarItem? Item { get; init; }

        public int DayIndex { get; init; }

        public DateTimeOffset StartUtc { get; set; }

        public DateTimeOffset EndUtc { get; set; }

        public TimeSpan GrabOffset { get; init; }

        public bool IsCopy { get; init; }
    }
}
