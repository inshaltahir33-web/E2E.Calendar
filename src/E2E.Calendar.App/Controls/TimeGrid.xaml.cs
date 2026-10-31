using System.Collections.Specialized;
using E2E.Calendar.App.Utilities;
using E2E.Calendar.Core;
using E2E.Calendar.Core.Models;
using E2E.Calendar.Core.Services;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
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

    public static readonly DependencyProperty WeekStartProperty = DependencyProperty.Register(
        nameof(WeekStart),
        typeof(WeekStartDay),
        typeof(TimeGrid),
        new PropertyMetadata(WeekStartDay.Monday, OnLayoutSourceChanged));

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

    public static readonly DependencyProperty WeatherTextProperty = DependencyProperty.Register(
        nameof(WeatherText),
        typeof(string),
        typeof(TimeGrid),
        new PropertyMetadata(null, OnWeatherTextChanged));

    private const int HoursPerDay = 24;
    private const int BannerLanes = 3;
    private const int WeekViewMaxColumns = 3;
    private const int DayViewMaxColumns = 4;
    private const double MinChipHeight = 22;
    private const double ResizeEdgeInPixels = 6;
    private const double SecondaryColumnWidth = 72;
    private const int SnapMinutes = 15;
    private const int FineSnapMinutes = 5;

    private double _sessionScrollOffset = -1;

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
    private CalendarLayoutResult? _layout;
    private int _layoutRevision;
    private IReadOnlyDictionary<string, CalendarItem> _itemsById =
        new Dictionary<string, CalendarItem>(StringComparer.Ordinal);
    private long _lastCreateTick;

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
        ActualThemeChanged += (_, _) => OnThemeChanged();
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

    public WeekStartDay WeekStart
    {
        get => (WeekStartDay)GetValue(WeekStartProperty);
        set => SetValue(WeekStartProperty, value);
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

    public string? WeatherText
    {
        get => (string?)GetValue(WeatherTextProperty);
        set => SetValue(WeatherTextProperty, value);
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

    private static void OnWeatherTextChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        if (sender is TimeGrid grid)
        {
            grid.RenderWeatherChip();
        }
    }

    /// <summary>
    /// Renders the compact weather chip (icon, temperature, condition) at the top-right of
    /// the header. The service still returns one display string, so it is split here; when
    /// weather is unavailable the chip simply disappears.
    /// </summary>
    private void RenderWeatherChip()
    {
        if (WeatherChip is null)
        {
            return;
        }

        var text = WeatherText;

        if (string.IsNullOrWhiteSpace(text) ||
            string.Equals(text, "Weather unavailable", StringComparison.OrdinalIgnoreCase))
        {
            WeatherChip.Visibility = Visibility.Collapsed;
            return;
        }

        var parts = text.Split('·', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var temperature = parts.FirstOrDefault(part => part.Contains('°'));
        var condition = temperature is null
            ? null
            : parts.SkipWhile(part => !string.Equals(part, temperature, StringComparison.Ordinal)).Skip(1).FirstOrDefault();

        WeatherTemperatureText.Text = temperature ?? text;
        WeatherConditionText.Text = condition ?? string.Empty;
        WeatherConditionText.Visibility = string.IsNullOrEmpty(condition) ? Visibility.Collapsed : Visibility.Visible;
        WeatherIcon.Glyph = WeatherGlyph(condition);
        WeatherChip.Visibility = Visibility.Visible;

        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(WeatherChip, text);
        ToolTipService.SetToolTip(WeatherChip, text);
    }

    private static string WeatherGlyph(string? condition) => condition switch
    {
        "Clear" => "\uE706",
        _ => "\uE753",
    };

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

    /// <summary>
    /// Brushes are baked into code-built elements, so a theme switch must discard the cached
    /// render signatures and rebuild the layers; otherwise the grid keeps the previous theme.
    /// </summary>
    private void OnThemeChanged()
    {
        _headerSignature = null;
        _allDaySignature = null;
        _bodySignature = null;
        _linesSignature = null;
        _layersHourHeight = -1;
        RequestRebuild();
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
            _layout = null;
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
        BuildLayout(days);

        RenderWeatherChip();
        ApplyDayTints(days);
        RenderHeader(days);
        RenderAllDayStrip();
        RenderBody();
        RenderNowLine();
        UpdateEmptyHint();
        UpdateScrollIndicator();
        EnsureInitialScroll(days);
    }

    /// <summary>
    /// The only source of placement geometry for the strip and the body: all-day
    /// items and items longer than a day become week bars, everything else is
    /// split per day and packed into columns.
    /// </summary>
    private void BuildLayout(IReadOnlyList<TimeGridDay> days)
    {
        var items = Items ?? Array.Empty<CalendarItem>();
        var byId = new Dictionary<string, CalendarItem>(items.Count, StringComparer.Ordinal);
        var occurrences = new List<LayoutOccurrence>(items.Count);

        foreach (var item in items)
        {
            byId[item.Id] = item;
            occurrences.Add(CalendarLayout.ToOccurrence(item, LocalTimeZone));
        }

        _itemsById = byId;
        _layout = CalendarLayout.Build(
            occurrences,
            days[0].Date,
            days[^1].Date,
            WeekStart,
            maxBannerLanes: BannerLanes,
            maxColumns: days.Count == 1 ? DayViewMaxColumns : WeekViewMaxColumns);
        _layoutRevision++;
    }

    /// <summary>
    /// Today's column is tinted AccentSoft and weekend columns slightly darker. The tint is
    /// reapplied on every rebuild so it follows week/month navigation instead of freezing on
    /// the days that were visible when the canvases were created. The today tint only applies
    /// to multi-day views (week): a single-day Day view shows one column and skips the tint.
    /// </summary>
    private void ApplyDayTints(IReadOnlyList<TimeGridDay> days)
    {
        // Day view renders a single day column, so only week views tint the today column.
        var showTodayTint = days.Count > 1;

        for (var index = 0; index < _dayCanvases.Count && index < days.Count; index++)
        {
            _dayCanvases[index].Background = days[index].IsToday && showTodayTint
                ? ThemeBrushes.Get(this, "AccentSoft")
                : days[index].IsWeekend
                    ? ThemeBrushes.Get(this, "App.WeekendTintBrush")
                    : ThemeBrushes.Get(this, "App.TransparentBrush");
        }
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

    private void ApplySharedColumns(int dayCount)
    {
        var total = dayCount + 1 + (ShowSecondaryTimeZone ? 1 : 0);
        var signature = (dayCount * 2) + (ShowSecondaryTimeZone ? 1 : 0);

        if (_columnsSignature == signature)
        {
            Grid.SetColumnSpan(BodyScroll, total);
            Grid.SetColumnSpan(EmptyHint, total);
            Grid.SetColumnSpan(WeatherChip, total);
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
        Grid.SetColumnSpan(WeatherChip, total);
        Grid.SetColumn(ScrollIndicator, total - 1);
    }

    private void CreateBodyLayers(int dayCount)
    {
        var total = dayCount + 1 + (ShowSecondaryTimeZone ? 1 : 0);

        BodyGrid.Children.Clear();
        BodyGrid.Height = HourHeight * HoursPerDay;

        _linesCanvas = new Canvas { IsHitTestVisible = false };
        Grid.SetColumn(_linesCanvas, 1);
        Grid.SetColumnSpan(_linesCanvas, dayCount + (ShowSecondaryTimeZone ? 1 : 0));
        BodyGrid.Children.Add(_linesCanvas);

        _dayCanvases.Clear();

        for (var index = 0; index < dayCount; index++)
        {
            var day = Days?[index];
            var canvas = new Canvas
            {
                Height = HourHeight * HoursPerDay,
                Width = ComputeColumnWidth(dayCount),
                Background = day?.IsToday == true
                    ? ThemeBrushes.Get(this, "AccentSoft")
                    : day?.IsWeekend == true
                        ? ThemeBrushes.Get(this, "App.WeekendTintBrush")
                        : ThemeBrushes.Get(this, "App.TransparentBrush"),
            };

            Grid.SetColumn(canvas, index + 1);
            canvas.PointerPressed += OnDayCanvasPointerPressed;
            canvas.PointerMoved += OnDayCanvasPointerMoved;
            canvas.PointerReleased += OnDayCanvasPointerReleased;
            canvas.PointerCaptureLost += OnDayCanvasPointerReleased;
            canvas.RightTapped += OnDayCanvasRightTapped;
            canvas.IsDoubleTapEnabled = true;
            canvas.DoubleTapped += OnDayCanvasDoubleTapped;
            BodyGrid.Children.Add(canvas);
            _dayCanvases.Add(canvas);
        }

        _gutterCanvas = new Canvas
        {
            Background = ThemeBrushes.Get(this, "App.TransparentBrush"),
        };
        Grid.SetColumn(_gutterCanvas, 0);
        BodyGrid.Children.Add(_gutterCanvas);

        _overlayCanvas = new Canvas { IsHitTestVisible = false };
        Grid.SetColumn(_overlayCanvas, 1);
        Grid.SetColumnSpan(_overlayCanvas, dayCount + (ShowSecondaryTimeZone ? 1 : 0));
        BodyGrid.Children.Add(_overlayCanvas);

        _nowLine = new Rectangle
        {
            Height = 2,
            Fill = ThemeBrushes.Get(this, "NowLine"),
            Visibility = Visibility.Collapsed,
        };

        _overlayCanvas.Children.Add(_nowLine);

        _nowDot = new Ellipse
        {
            Width = 8,
            Height = 8,
            Fill = ThemeBrushes.Get(this, "NowLine"),
            Visibility = Visibility.Collapsed,
        };

        _overlayCanvas.Children.Add(_nowDot);

        _ghost = new Border
        {
            Background = ThemeBrushes.Get(this, "App.AccentBrush"),
            BorderBrush = ThemeBrushes.Get(this, "App.AccentBrush"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(Tokens.Double("RadiusChip", 6d)),
            Opacity = 0.35,
            Visibility = Visibility.Collapsed,
        };

        _overlayCanvas.Children.Add(_ghost);

        _ghostLabel = new TextBlock
        {
            FontSize = Tokens.Double("FontSizeSectionHeader", 11d),
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = ThemeBrushes.Get(this, "App.TextPrimaryBrush"),
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
            // 10px keeps the whole "UTC+05:00" inside the 56px gutter instead of clipping to "TC+05:00".
            FontSize = Tokens.Double("FontSizeChipMeta", 10d),
            Foreground = ThemeBrushes.Get(this, "App.TextSecondaryBrush"),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom,
            TextAlignment = TextAlignment.Right,
            TextTrimming = TextTrimming.None,
            TextWrapping = TextWrapping.NoWrap,
            Width = Math.Max(0, GutterWidth - 4),
            Margin = new Thickness(0, 0, 4, 6),
        };

        Grid.SetRow(tzLabel, 1);
        Grid.SetColumn(tzLabel, 0);
        RootLayout.Children.Add(tzLabel);
        _headerCells.Add(tzLabel);

        for (var index = 0; index < days.Count; index++)
        {
            var day = days[index];
            var cell = CreateHeaderCell(day);
            Grid.SetRow(cell, 1);
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
            FontSize = Tokens.Double("FontSizeSectionHeader", 11d),
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            CharacterSpacing = 40,
            Foreground = ThemeBrushes.Get(this, "App.TextSecondaryBrush"),
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
            FontSize = Tokens.Double("FontSizeSubtitle", 20d),
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };

        if (day.IsToday)
        {
            dateHost.Background = ThemeBrushes.Get(this, "Accent");
            dateHost.CornerRadius = new CornerRadius(16);
            dateText.Foreground = ThemeBrushes.Get(this, "App.OnAccentBrush");
        }
        else
        {
            dateText.Foreground = ThemeBrushes.Get(this, "App.TextPrimaryBrush");
        }

        dateHost.Children.Add(dateText);
        content.Children.Add(dateHost);

        return new Border
        {
            Child = content,
            MinHeight = Tokens.Double("GridDayHeaderHeight", 60d),
            Padding = new Thickness(0, 4, 0, 4),
            BorderBrush = ThemeBrushes.Get(this, "App.StrokeSubtleBrush"),
            BorderThickness = new Thickness(0, 0, 0, 1),
        };
    }

    private void RenderAllDayStrip()
    {
        var days = Days ?? Array.Empty<TimeGridDay>();

        if (days.Count == 0)
        {
            return;
        }

        var signature = $"{DaySignature(days)}|{_layoutRevision}";

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
            FontSize = Tokens.Double("FontSizeSectionHeader", 11d),
            Foreground = ThemeBrushes.Get(this, "App.TextSecondaryBrush"),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 6, 8, 0),
        };

        Grid.SetRow(label, 2);
        Grid.SetColumn(label, 0);
        RootLayout.Children.Add(label);
        _allDayCells.Add(label);

        if (_layout is null)
        {
            return;
        }

        var dayIndexByDate = new Dictionary<DateOnly, int>(days.Count);

        for (var index = 0; index < days.Count; index++)
        {
            dayIndexByDate[days[index].Date] = index;
        }

        var hiddenByDay = new Dictionary<DateOnly, List<BannerPlacement>>();

        foreach (var week in _layout.Weeks)
        {
            foreach (var banner in week.Banners)
            {
                if (!dayIndexByDate.TryGetValue(banner.StartDay, out var startIndex))
                {
                    continue;
                }

                var endIndex = dayIndexByDate.TryGetValue(banner.EndDay, out var found)
                    ? found
                    : days.Count - 1;

                if (endIndex < startIndex)
                {
                    endIndex = startIndex;
                }

                var item = ResolveItem(banner.Occurrence);

                if (item is null)
                {
                    continue;
                }

                var bar = CreateAllDayBar(item, banner);
                Grid.SetRow(bar, 2 + banner.Lane);
                Grid.SetColumn(bar, startIndex + 1);
                Grid.SetColumnSpan(bar, endIndex - startIndex + 1);
                RootLayout.Children.Add(bar);
                _allDayCells.Add(bar);
            }

            foreach (var hidden in week.HiddenPlacements)
            {
                for (var day = hidden.StartDay; day <= hidden.EndDay; day = day.AddDays(1))
                {
                    if (!dayIndexByDate.ContainsKey(day))
                    {
                        continue;
                    }

                    if (!hiddenByDay.TryGetValue(day, out var list))
                    {
                        list = new List<BannerPlacement>();
                        hiddenByDay[day] = list;
                    }

                    list.Add(hidden);
                }
            }
        }

        foreach (var pair in hiddenByDay.OrderBy(entry => entry.Key))
        {
            var button = CreateOverflowButton(
                $"+{pair.Value.Count}",
                BuildBannerFlyoutRows(pair.Value),
                $"Show {pair.Value.Count} more all-day items");

            Grid.SetRow(button, 2 + BannerLanes);
            Grid.SetColumn(button, dayIndexByDate[pair.Key] + 1);
            button.HorizontalAlignment = HorizontalAlignment.Left;
            RootLayout.Children.Add(button);
            _allDayCells.Add(button);
        }
    }

    private Border CreateAllDayBar(CalendarItem item, BannerPlacement placement)
    {
        var chipBrushes = EventColorService.GetChipBrushes(item.Color, ActualTheme);
        var content = new Grid();
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(3) });
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var stripe = new Border { Background = chipBrushes.Stripe };
        Grid.SetColumn(stripe, 0);
        content.Children.Add(stripe);

        var text = new TextBlock
        {
            Text = item.Title,
            FontSize = Tokens.Double("FontSizeCaption", 12d),
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = chipBrushes.Text,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(6, 3, 4, 3),
        };

        Grid.SetColumn(text, 1);
        content.Children.Add(text);

        var radius = Tokens.Double("RadiusChip", 6d);

        var bar = new Border
        {
            Child = content,
            Background = chipBrushes.Fill,
            CornerRadius = new CornerRadius(
                placement.ContinuesFromPrev ? 0 : radius,
                placement.ContinuesToNext ? 0 : radius,
                placement.ContinuesToNext ? 0 : radius,
                placement.ContinuesFromPrev ? 0 : radius),
            Margin = new Thickness(
                placement.ContinuesFromPrev ? 0 : 2,
                1,
                placement.ContinuesToNext ? 0 : 2,
                1),
            Tag = item,
            IsTabStop = true,
        };

        AutomationProperties.SetName(bar, item.Title);
        bar.Tapped += OnAllDayBarTapped;
        bar.KeyDown += OnAllDayBarKeyDown;

        return bar;
    }

    private void OnAllDayBarTapped(object sender, TappedRoutedEventArgs e)
    {
        if (sender is Border { Tag: CalendarItem item })
        {
            ItemActivated?.Invoke(this, new TimeGridItemEventArgs(item));
            e.Handled = true;
        }
    }

    private void OnAllDayBarKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key is VirtualKey.Enter or VirtualKey.Space && sender is Border { Tag: CalendarItem item })
        {
            ItemActivated?.Invoke(this, new TimeGridItemEventArgs(item));
            e.Handled = true;
        }
    }

    private void RenderBody()
    {
        var days = Days ?? Array.Empty<TimeGridDay>();

        if (days.Count == 0 || _dayCanvases.Count == 0)
        {
            return;
        }

        _columnWidth = ComputeColumnWidth(days.Count);

        var signature = string.Join(
            '|',
            DaySignature(days),
            _layoutRevision,
            SelectedItemId,
            ClockFormat,
            Math.Round(_columnWidth, 1),
            Math.Round(HourHeight, 1));

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

        if (_layout is null)
        {
            RenderLines();
            RenderNowLine();
            return;
        }

        var firstDate = days[0].Date;

        foreach (var day in _layout.Days)
        {
            var dayIndex = day.Date.DayNumber - firstDate.DayNumber;

            if (dayIndex < 0 || dayIndex >= _dayCanvases.Count)
            {
                continue;
            }

            var canvas = _dayCanvases[dayIndex];
            var dayStart = day.Date.ToDateTime(TimeOnly.MinValue);

            foreach (var placement in day.Items)
            {
                var item = ResolveItem(placement.Occurrence);

                if (item is null)
                {
                    continue;
                }

                var chip = CreateChip(item, days[dayIndex]);

                var top = (placement.VisualStart - dayStart).TotalHours * HourHeight;
                var height = item.IsTickable
                    ? MinChipHeight
                    : Math.Max(
                        MinChipHeight,
                        (placement.VisualEnd - placement.VisualStart).TotalHours * HourHeight - 2);
                var columnCount = Math.Max(1, placement.ColumnCount);
                var sliceWidth = _columnWidth / columnCount;

                Canvas.SetTop(chip, top);
                Canvas.SetLeft(chip, (placement.Column * sliceWidth) + 1);
                chip.Width = Math.Max(24, sliceWidth - 2);
                chip.Height = height;
                canvas.Children.Add(chip);
            }

            if (day.OverflowCount > 0)
            {
                var more = CreateOverflowButton(
                    $"+{day.OverflowCount}",
                    BuildTimedFlyoutRows(day.Hidden),
                    $"Show {day.OverflowCount} more items");

                Canvas.SetLeft(more, Math.Max(2, _columnWidth - 46));
                Canvas.SetTop(more, Math.Max(0, (HoursPerDay * HourHeight) - MinChipHeight - 2));
                canvas.Children.Add(more);
            }
        }

        RenderLines();
        RenderNowLine();
    }

    private CalendarItem? ResolveItem(LayoutOccurrence occurrence) =>
        _itemsById.TryGetValue(occurrence.Id, out var item) ? item : null;

    private List<HiddenItemRow> BuildBannerFlyoutRows(IEnumerable<BannerPlacement> hidden) =>
        hidden
            .Select(placement => ResolveItem(placement.Occurrence))
            .Where(item => item is not null)
            .Select(item => new HiddenItemRow($"{item!.Title} · All day"))
            .ToList();

    private List<HiddenItemRow> BuildTimedFlyoutRows(IEnumerable<TimedPlacement> hidden) =>
        hidden
            .Select(placement => ResolveItem(placement.Occurrence))
            .Where(item => item is not null)
            .Select(item => new HiddenItemRow($"{FormatTime(item!.StartUtc)}  {item.Title}"))
            .ToList();

    /// <summary>
    /// The "+N" chip for lanes/columns that did not fit. It opens the same kind
    /// of list the month cell uses, so hidden items are still reachable.
    /// </summary>
    private Button CreateOverflowButton(
        string text,
        IReadOnlyList<HiddenItemRow> rows,
        string automationName)
    {
        var button = new Button
        {
            Content = text,
            FontSize = Tokens.Double("FontSizeSectionHeader", 11d),
            Padding = new Thickness(8, 0, 8, 0),
            MinHeight = MinChipHeight,
            Height = MinChipHeight,
            Background = ThemeBrushes.Get(this, "App.CardElevatedBrush"),
            BorderBrush = ThemeBrushes.Get(this, "App.DividerBrush"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(MinChipHeight / 2),
            Foreground = ThemeBrushes.Get(this, "App.TextSecondaryBrush"),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(2, 1, 2, 1),
        };

        button.Click += (_, _) =>
        {
            var flyout = new Flyout
            {
                Placement = FlyoutPlacementMode.BottomEdgeAlignedLeft,
                Content = new ListView
                {
                    ItemsSource = rows,
                    SelectionMode = ListViewSelectionMode.None,
                    MaxHeight = 320,
                    DisplayMemberPath = nameof(HiddenItemRow.Label),
                },
            };

            OverlayFocus.ShowAt(flyout, button);
        };

        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(button, automationName);

        return button;
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

        var divider = ThemeBrushes.Get(this, "GridLine");
        var halfHour = ThemeBrushes.Get(this, "App.StrokeGridHalfBrush");
        var days = Days ?? Array.Empty<TimeGridDay>();

        for (var hour = 0; hour <= HoursPerDay; hour++)
        {
            var y = hour * HourHeight;

            var line = new Rectangle
            {
                Height = 1,
                Width = Math.Max(0, width - GutterWidth),
                Fill = divider,
            };

            Canvas.SetTop(line, y);
            _linesCanvas.Children.Add(line);

            if (hour < HoursPerDay)
            {
                var halfLine = new Rectangle
                {
                    Height = 1,
                    Width = Math.Max(0, width - GutterWidth),
                    Fill = halfHour,
                };

                Canvas.SetTop(halfLine, y + (HourHeight / 2));
                _linesCanvas.Children.Add(halfLine);
            }

            if (hour == 0 || hour == HoursPerDay)
            {
                continue;
            }

            var label = new TextBlock
            {
                Text = FormatHour(hour),
                FontSize = Tokens.Double("FontSizeHourLabel", 11d),
                Foreground = ThemeBrushes.Get(this, "App.TextSecondaryBrush"),
                HorizontalAlignment = HorizontalAlignment.Right,
                TextAlignment = TextAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                Height = 18,
            };

            Canvas.SetTop(label, y - (label.Height / 2));
            Canvas.SetLeft(label, 0);
            label.Width = Math.Max(0, GutterWidth - 8);
            _gutterCanvas.Children.Add(label);
        }

        if (days.Count > 1)
        {
            var verticalBrush = ThemeBrushes.Get(this, "App.GridVerticalBrush");
            for (var index = 1; index < days.Count; index++)
            {
                var separator = new Rectangle
                {
                    Width = 1,
                    Height = HourHeight * HoursPerDay,
                    Fill = verticalBrush,
                    Opacity = 1,
                };

                Canvas.SetLeft(separator, index * _columnWidth);
                _linesCanvas.Children.Add(separator);
            }
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
                FontSize = Tokens.Double("FontSizeCaption", 12d),
                Foreground = ThemeBrushes.Get(this, "App.TextSecondaryBrush"),
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
        var left = todayIndex * _columnWidth;

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

        if (days.Count == 0 || _layout is null)
        {
            EmptyHint.Visibility = Visibility.Collapsed;
            return;
        }

        var hasContent = _layout.Weeks.Any(week => week.Banners.Count > 0)
            || _layout.Days.Any(day => day.Items.Count > 0);

        EmptyHint.Visibility = hasContent ? Visibility.Collapsed : Visibility.Visible;
        PositionEmptyHint();
    }

    /// <summary>
    /// Parks the empty state in the top third of the visible grid so it can never sit on top
    /// of the now line, which starts around 90 minutes below the initial scroll position.
    /// </summary>
    private void PositionEmptyHint()
    {
        var viewport = BodyScroll.ViewportHeight > 0 ? BodyScroll.ViewportHeight : ActualHeight;

        if (viewport <= 0)
        {
            viewport = 600;
        }

        var top = Math.Max(24, (viewport / 3d) - 40d);
        EmptyHint.Margin = new Thickness(0, top, 0, 0);
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
        if (_sessionScrollOffset >= 0)
        {
            ApplyScroll(_sessionScrollOffset);
            return;
        }

        var now = Now ?? DateTimeOffset.Now;
        var today = DateOnly.FromDateTime(LocalTimeZone.ToLocal(now));
        var isTodayVisible = days.Any(day => day.Date == today);

        // Initial position: keep "now" about two hours below the top edge (never above 06:00 at
        // the top), or start at 08:00 when the selected day is not today.
        var offset = isTodayVisible
            ? GetInitialOffset(now, today)
            : 8 * HourHeight;

        if (_hasScrolled && !isTodayVisible)
        {
            // The user already browsed away from today, so keep their scroll position.
            return;
        }

        _hasScrolled = true;
        ApplyScroll(Math.Max(0, offset));
        _sessionScrollOffset = -1;
    }

    /// <summary>
    /// Offset that places the current time two hours below the top edge of the grid, clamped so
    /// the top edge is never above 06:00.
    /// </summary>
    private double GetInitialOffset(DateTimeOffset now, DateOnly today)
    {
        var dayStartUtc = DayStartUtc(today);
        var topUtc = now.AddMinutes(-120);
        var minTopUtc = dayStartUtc.AddHours(6);

        if (topUtc < minTopUtc)
        {
            topUtc = minTopUtc;
        }

        return (topUtc - dayStartUtc).TotalHours * HourHeight;
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

        if (EmptyHint.Visibility == Visibility.Visible)
        {
            PositionEmptyHint();
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

        OverlayFocus.ShowAt(flyout, chip, new FlyoutShowOptions { Position = e.GetPosition(chip) });
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

        foreach (var paletteName in CalendarPalette.All)
        {
            var entry = new MenuFlyoutItem { Text = paletteName };
            entry.Click += (_, _) => ItemActionRequested?.Invoke(this, new TimeGridItemActionEventArgs(item with { Color = paletteName }, TimeGridItemAction.ChangeColor));
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

        OverlayFocus.ShowAt(flyout, canvas, new FlyoutShowOptions { Position = e.GetPosition(canvas) });
    }

    private void OnDayCanvasDoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
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

        // A drag-create on this spot already opened the editor.
        if (Environment.TickCount64 - _lastCreateTick < 400)
        {
            e.Handled = true;
            return;
        }

        var position = e.GetPosition(canvas);

        if (e.OriginalSource is DependencyObject source && FindAncestor<Button>(source) is not null)
        {
            return;
        }

        if (HitTest(canvas, position) is not null)
        {
            return;
        }

        var start = TimeAt(dayIndex, position.Y);
        SlotCreated?.Invoke(this, new TimeGridSlotEventArgs(start, start.AddHours(1)));
        e.Handled = true;
    }

    private void OnChipPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (sender is not EventChipView chip || chip.Tag is not CalendarItem item)
        {
            return;
        }

        if (FindAncestor<Canvas>(chip) is Canvas canvas)
        {
            BeginItemDrag(canvas, item, e.GetCurrentPoint(canvas).Position, e.Pointer);
        }

        e.Handled = true;
    }

    private void OnDayCanvasPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (_dayCanvases.Count == 0 ||
            e.OriginalSource is DependencyObject source && FindAncestor<Button>(source) is not null)
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
            BeginItemDrag(canvas, hitItem, position, e.Pointer);
        }
        else
        {
            // A plain click only focuses the grid; creation happens on a drag or
            // a double-click, both anchored to the snapped slot under the pointer.
            var time = TimeAt(dayIndex, position.Y);

            _drag = new DragState
            {
                Mode = DragMode.Create,
                DayIndex = dayIndex,
                AnchorUtc = time,
                StartUtc = time,
                EndUtc = time.AddMinutes(SnapMinutes),
            };
        }

        canvas.CapturePointer(e.Pointer);
    }

    private void BeginItemDrag(Canvas canvas, CalendarItem item, Point position, Pointer pointer)
    {
        var dayIndex = _dayCanvases.IndexOf(canvas);
        var hit = HitTest(canvas, position);
        var mode = hit is not null && IsOnResizeEdge(hit, position) ? DragMode.Resize : DragMode.Move;
        _drag = new DragState
        {
            Mode = mode,
            Item = item,
            DayIndex = dayIndex,
            StartUtc = item.StartUtc,
            EndUtc = item.EndUtc,
            GrabOffset = TimeAt(dayIndex, position.Y) - item.StartUtc,
            IsCopy = mode == DragMode.Move && IsControlDown(),
            LastPointerPosition = position,
        };
        canvas.CapturePointer(pointer);
        ShowPreview();
    }

    private static T? FindAncestor<T>(DependencyObject element) where T : DependencyObject
    {
        while (element is not null)
        {
            if (element is T match)
            {
                return match;
            }

            element = VisualTreeHelper.GetParent(element);
        }

        return null;
    }

    private void OnDayCanvasPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (_drag is null)
        {
            return;
        }

        var canvas = (Canvas)sender;
        var position = e.GetCurrentPoint(canvas).Position;
        if (Math.Abs(position.X - _drag.LastPointerPosition.X) > 3 ||
            Math.Abs(position.Y - _drag.LastPointerPosition.Y) > 3)
        {
            _drag.HasMoved = true;
        }
        _drag.LastPointerPosition = position;
        var time = TimeAt(_drag.DayIndex, position.Y);

        switch (_drag.Mode)
        {
            case DragMode.Create:
                var anchor = _drag.AnchorUtc;
                _drag.StartUtc = time < anchor ? time : anchor;
                _drag.EndUtc = time < anchor ? anchor : time;

                if (_drag.EndUtc <= _drag.StartUtc)
                {
                    _drag.EndUtc = _drag.StartUtc.AddMinutes(SnapMinutes);
                }

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
                if (drag.HasMoved)
                {
                    _lastCreateTick = Environment.TickCount64;
                    SlotCreated?.Invoke(this, new TimeGridSlotEventArgs(drag.StartUtc, drag.EndUtc));
                }

                break;

            case DragMode.Move:
                if (!drag.HasMoved)
                {
                    ItemActivated?.Invoke(this, new TimeGridItemEventArgs(drag.Item!));
                    break;
                }
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
        Canvas.SetLeft(_ghost, (_drag.DayIndex * _columnWidth) + 2);

        _ghostLabel.Visibility = Visibility.Visible;
        _ghostLabel.Text = $"{FormatTime(_drag.StartUtc)} – {FormatTime(_drag.EndUtc)}";
        Canvas.SetTop(_ghostLabel, top - 16);
        Canvas.SetLeft(_ghostLabel, (_drag.DayIndex * _columnWidth) + 4);
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

    private DateTimeOffset DayStartUtc(DateOnly date)
    {
        return LocalTimeZone.ToUtc(date.ToDateTime(TimeOnly.MinValue));
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
        var absolute = offset.Duration();

        // Format as "UTC+05:00" for consistency with UI design tokens
        var sign = offset < TimeSpan.Zero ? "-" : "+";
        return $"UTC{sign}{absolute.Hours:00}:{absolute.Minutes:00}";
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

        public DateTimeOffset AnchorUtc { get; init; }

        public DateTimeOffset StartUtc { get; set; }

        public DateTimeOffset EndUtc { get; set; }

        public TimeSpan GrabOffset { get; init; }

        public bool IsCopy { get; init; }

        public Point LastPointerPosition { get; set; }

        public bool HasMoved { get; set; }
    }

    private sealed record HiddenItemRow(string Label);
}
