using System.Collections.Specialized;
using E2E.Calendar.App.Utilities;
using E2E.Calendar.Core;
using E2E.Calendar.Core.Models;
using E2E.Calendar.Core.Services;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
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
        new PropertyMetadata(48d, OnLayoutSourceChanged));

    public static readonly DependencyProperty NowProperty = DependencyProperty.Register(
        nameof(Now),
        typeof(DateTimeOffset?),
        typeof(TimeGrid),
        new PropertyMetadata(null, OnLayoutSourceChanged));

    private const double GutterWidth = 48;
    private const double ResizeEdgeInPixels = 6;
    private const double SnapMinutes = 15;

    private readonly List<Canvas> _dayCanvases = new();
    private readonly DispatcherTimer _nowTimer;

    private DragState? _drag;
    private Border? _dragPreview;
    private Canvas? _dragPreviewHost;
    private double _columnWidth;

    public TimeGrid()
    {
        InitializeComponent();

        _nowTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
        _nowTimer.Tick += OnNowTimerTick;
        _nowTimer.Start();

        ScrollViewer.SizeChanged += OnScrollViewerSizeChanged;
        Unloaded += OnUnloaded;
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

    public event EventHandler<TimeGridItemEventArgs>? ItemActivated;

    public event EventHandler<TimeGridSlotEventArgs>? SlotCreated;

    public event EventHandler<TimeGridItemEventArgs>? ItemMoved;

    public event EventHandler<TimeGridItemEventArgs>? ItemResized;

    public event EventHandler<TimeGridItemEventArgs>? ItemCopied;

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

        grid.Rebuild();
    }

    private void OnItemsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RenderAllDayStrip();
        RenderItems();
        RenderNowLine();
    }

    private static TimeZoneInfo LocalTimeZone => TimeZoneInfo.Local;

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _nowTimer.Stop();
    }

    private void OnNowTimerTick(object? sender, object e)
    {
        if (Now is null)
        {
            return;
        }

        RenderNowLine();
    }

    private void OnScrollViewerSizeChanged(object sender, SizeChangedEventArgs args)
    {
        Rebuild();
    }

    private void OnViewChanged(object sender, ScrollViewerViewChangedEventArgs args)
    {
        RenderItems();
    }

    private void Rebuild()
    {
        var days = Days ?? Array.Empty<TimeGridDay>();

        _dayCanvases.Clear();
        ColumnsHost.ColumnDefinitions.Clear();
        ColumnsHost.Children.Clear();
        HeaderHost.ColumnDefinitions.Clear();
        HeaderHost.Children.Clear();
        AllDayHost.ColumnDefinitions.Clear();
        AllDayHost.Children.Clear();

        HeaderHost.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(GutterWidth) });
        AllDayHost.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(GutterWidth) });

        var viewportWidth = ScrollViewer.ViewportWidth > 0 ? ScrollViewer.ViewportWidth : ActualWidth;
        _columnWidth = days.Count > 0 ? Math.Max(0, (viewportWidth - GutterWidth) / days.Count) : 0;

        foreach (var day in days)
        {
            HeaderHost.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(_columnWidth) });
            AllDayHost.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(_columnWidth) });

            var header = new TextBlock
            {
                Text = day.Header,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 4),
                Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
            };

            Grid.SetColumn(header, HeaderHost.ColumnDefinitions.Count - 1);
            HeaderHost.Children.Add(header);

            var dayCanvas = new Canvas
            {
                Height = HourHeight * 24,
                Width = _columnWidth,
            };

            Grid.SetColumn(dayCanvas, ColumnsHost.ColumnDefinitions.Count);
            ColumnsHost.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(_columnWidth) });
            ColumnsHost.Children.Add(dayCanvas);

            _dayCanvases.Add(dayCanvas);

            dayCanvas.PointerPressed += OnDayCanvasPointerPressed;
            dayCanvas.PointerMoved += OnDayCanvasPointerMoved;
            dayCanvas.PointerReleased += OnDayCanvasPointerReleased;
            dayCanvas.PointerCaptureLost += OnDayCanvasPointerReleased;
        }

        TimeHost.Height = HourHeight * 24;
        LinesCanvas.Height = HourHeight * 24;
        LinesCanvas.Width = Math.Max(viewportWidth, GutterWidth);

        RenderHourGrid();
        RenderAllDayStrip();
        RenderItems();
        RenderNowLine();
    }

    private void RenderHourGrid()
    {
        LinesCanvas.Children.Clear();
        GutterCanvas.Children.Clear();

        for (var hour = 0; hour <= 24; hour++)
        {
            var y = hour * HourHeight;

            var line = new Rectangle
            {
                Height = 1,
                Width = LinesCanvas.Width,
                Fill = (Brush)Application.Current.Resources["DividerStrokeColorDefaultBrush"],
            };

            Canvas.SetTop(line, y);
            LinesCanvas.Children.Add(line);

            if (hour == 24)
            {
                continue;
            }

            var label = new TextBlock
            {
                Text = hour.ToString("00"),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 2, 6, 0),
                Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
            };

            Canvas.SetTop(label, y);
            GutterCanvas.Children.Add(label);
        }
    }

    private void RenderAllDayStrip()
    {
        var days = Days ?? Array.Empty<TimeGridDay>();
        var allDayItems = (Items ?? Array.Empty<CalendarItem>())
            .Where(item => item.AllDay)
            .ToList();

        foreach (var child in AllDayHost.Children.Skip(1).ToList())
        {
            AllDayHost.Children.Remove(child);
        }

        for (var index = 0; index < days.Count; index++)        {
            var column = new StackPanel
            {
                Orientation = Orientation.Vertical,
                Spacing = 2,
                Margin = new Thickness(2, 2, 2, 4),
            };

            foreach (var item in allDayItems.Where(item => DayIndexFor(item, days) == index).Take(3))
            {
                column.Children.Add(CreateChip(item, isAllDay: true));
            }

            Grid.SetColumn(column, index + 1);
            AllDayHost.Children.Add(column);
        }
    }

    private void RenderItems()
    {
        var days = Days ?? Array.Empty<TimeGridDay>();

        foreach (var canvas in _dayCanvases)
        {
            canvas.Children.Clear();
        }

        if (days.Count == 0)
        {
            return;
        }

        var visible = VisibleRange();
        var timed = (Items ?? Array.Empty<CalendarItem>())
            .Where(item => !item.AllDay)
            .Where(item => item.EndUtc > visible.Start && item.StartUtc < visible.End)
            .ToList();

        for (var dayIndex = 0; dayIndex < days.Count; dayIndex++)
        {
            var dayStart = DayStartUtc(days[dayIndex].Date);
            var dayItems = timed.Where(item => item.StartUtc < DayStartUtc(days[dayIndex].Date).AddDays(1)).ToList();

            var packed = OverlapLayout.Pack(dayItems, dayStart, dayStart.AddDays(1));

            foreach (var entry in packed)
            {
                var canvas = _dayCanvases[dayIndex];
                var chip = CreateChip(entry.Item, isAllDay: false);

                var y = (entry.Item.StartUtc - dayStart).TotalHours * HourHeight;
                var height = Math.Max(18, (entry.Item.EndUtc - entry.Item.StartUtc).TotalHours * HourHeight - 2);

                Canvas.SetTop(chip, y);
                Canvas.SetLeft(chip, entry.Column * (_columnWidth / entry.ColumnCount));
                chip.Width = Math.Max(24, (_columnWidth / entry.ColumnCount) - 2);
                chip.Height = height;

                chip.Tag = entry.Item;
                chip.PointerPressed += OnChipPointerPressed;

                canvas.Children.Add(chip);
            }
        }
    }

    private void RenderNowLine()
    {
        var days = Days ?? Array.Empty<TimeGridDay>();
        var now = Now ?? DateTimeOffset.Now;

        var existing = LinesCanvas.Children.OfType<Rectangle>().Where(line => Equals(line.Tag, "now")).ToList();

        foreach (var line in existing)
        {
            LinesCanvas.Children.Remove(line);
        }

        var todayIndex = days.ToList().FindIndex(day => day.Date == DateOnly.FromDateTime(LocalTimeZone.ToLocal(now)));

        if (todayIndex < 0 || todayIndex >= _dayCanvases.Count)
        {
            return;
        }

        var dayStart = DayStartUtc(days[todayIndex].Date);
        var y = (now - dayStart).TotalHours * HourHeight;

        var nowLine = new Rectangle
        {
            Height = 2,
            Width = _columnWidth,
            Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0xE8, 0x11, 0x23)),
            Tag = "now",
        };

        Canvas.SetTop(nowLine, y);
        Canvas.SetLeft(nowLine, todayIndex * _columnWidth);
        LinesCanvas.Children.Add(nowLine);
    }

    private Border CreateChip(CalendarItem item, bool isAllDay)
    {
        var accent = ColorHelper.FromHex(item.Color);
        var content = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = new GridLength(3) },
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
            },
        };

        var edge = new Border
        {
            Background = accent,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };

        Grid.SetColumn(edge, 0);
        content.Children.Add(edge);

        var text = new TextBlock
        {
            Text = item.Title,
            Margin = new Thickness(4, 1, 2, 1),
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
            FontSize = 12,
            Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
        };

        Grid.SetColumn(text, 1);
        content.Children.Add(text);

        return new Border
        {
            Child = content,
            CornerRadius = new CornerRadius(4),
            Background = (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
            BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
            BorderThickness = new Thickness(0, 0, 1, 1),
            Opacity = item.IsDone ? 0.5 : 1,
            Tag = item,
        };
    }

    private (DateTimeOffset Start, DateTimeOffset End) VisibleRange()
    {
        var scrollOffset = ScrollViewer.VerticalOffset;
        var viewportHeight = ScrollViewer.ViewportHeight > 0 ? ScrollViewer.ViewportHeight : ActualHeight;

        var firstDay = Days is { Count: > 0 } ? DayStartUtc(Days[0].Date) : DateTimeOffset.UnixEpoch;
        var start = firstDay.AddMinutes(scrollOffset / HourHeight * 60);
        var end = start.AddMinutes(viewportHeight / HourHeight * 60);

        return (start, end);
    }

    private DateTimeOffset DayStartUtc(DateOnly date)
    {
        var local = date.ToDateTime(TimeOnly.MinValue);

        return LocalTimeZone.ToUtc(local);
    }

    private int DayIndexFor(CalendarItem item, IReadOnlyList<TimeGridDay> days)
    {
        var local = DateOnly.FromDateTime(LocalTimeZone.ToLocal(item.StartUtc));

        for (var index = 0; index < days.Count; index++)
        {
            if (days[index].Date == local)
            {
                return index;
            }
        }

        return -1;
    }

    private void OnChipPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (sender is not Border chip || chip.Tag is not CalendarItem item)
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

        var position = e.GetCurrentPoint(canvas).Position;
        var hit = HitTest(canvas, position);

        if (hit is not null)
        {
            var item = (CalendarItem)hit.Tag!;
            var mode = IsOnResizeEdge(hit, position) ? DragMode.Resize : DragMode.Move;

            _drag = new DragState
            {
                Mode = mode,
                Item = item,
                DayIndex = dayIndex,
                StartUtc = item.StartUtc,
                EndUtc = item.EndUtc,
                GrabOffset = (TimeAt(dayIndex, position.Y) - item.StartUtc),
                IsCopy = mode == DragMode.Move && IsControlDown(),
            };

            canvas.CapturePointer(e.Pointer);
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

            canvas.CapturePointer(e.Pointer);
        }

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

    private void ShowPreview()
    {
        if (_drag is null)
        {
            return;
        }

        if (_dragPreview is null)
        {
            _dragPreview = new Border
            {
                Background = new SolidColorBrush(Windows.UI.Color.FromArgb(0x40, 0x00, 0x78, 0xD4)),
                BorderBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0x00, 0x78, 0xD4)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
            };

            _dragPreviewHost = _dayCanvases[_drag.DayIndex];
            _dragPreviewHost.Children.Add(_dragPreview);
        }

        var dayStart = DayStartUtc((Days ?? Array.Empty<TimeGridDay>())[_drag.DayIndex].Date);

        _dragPreview.Width = Math.Max(24, _columnWidth - 4);
        _dragPreview.Height = Math.Max(18, (_drag.EndUtc - _drag.StartUtc).TotalHours * HourHeight);

        Canvas.SetTop(_dragPreview, (_drag.StartUtc - dayStart).TotalHours * HourHeight);
        Canvas.SetLeft(_dragPreview, 2);
    }

    private void HidePreview()
    {
        if (_dragPreview is not null)
        {
            _dragPreviewHost?.Children.Remove(_dragPreview);
            _dragPreview = null;
            _dragPreviewHost = null;
        }
    }

    private Border? HitTest(Canvas canvas, Point position)
    {
        foreach (var child in canvas.Children.OfType<Border>())
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

    private static bool IsOnResizeEdge(Border chip, Point position)
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
        var snapped = Math.Round(minutes / SnapMinutes) * SnapMinutes;

        return dayStart.AddMinutes(snapped);
    }

    private static bool IsControlDown() =>
        (InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control) & Windows.UI.Core.CoreVirtualKeyStates.Down) != 0;

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
