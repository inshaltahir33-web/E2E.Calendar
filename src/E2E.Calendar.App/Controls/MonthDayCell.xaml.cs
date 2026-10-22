using E2E.Calendar.App.ViewModels;
using E2E.Calendar.Core.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Windows.ApplicationModel.DataTransfer;

namespace E2E.Calendar.App.Controls;

public sealed partial class MonthDayCell : UserControl
{
    public static readonly DependencyProperty DayNumberProperty = DependencyProperty.Register(
        nameof(DayNumber),
        typeof(string),
        typeof(MonthDayCell),
        new PropertyMetadata(string.Empty, OnVisualChanged));

    public static readonly DependencyProperty IsTodayProperty = DependencyProperty.Register(
        nameof(IsToday),
        typeof(bool),
        typeof(MonthDayCell),
        new PropertyMetadata(false, OnVisualChanged));

    public static readonly DependencyProperty IsCurrentMonthProperty = DependencyProperty.Register(
        nameof(IsCurrentMonth),
        typeof(bool),
        typeof(MonthDayCell),
        new PropertyMetadata(true, OnVisualChanged));

    public static readonly DependencyProperty IsWeekendProperty = DependencyProperty.Register(
        nameof(IsWeekend),
        typeof(bool),
        typeof(MonthDayCell),
        new PropertyMetadata(false, OnVisualChanged));

    public static readonly DependencyProperty IsSelectedProperty = DependencyProperty.Register(
        nameof(IsSelected),
        typeof(bool),
        typeof(MonthDayCell),
        new PropertyMetadata(false, OnVisualChanged));

    public static readonly DependencyProperty MoreLabelProperty = DependencyProperty.Register(
        nameof(MoreLabel),
        typeof(string),
        typeof(MonthDayCell),
        new PropertyMetadata(string.Empty, OnVisualChanged));

    public static readonly DependencyProperty MoreVisibleProperty = DependencyProperty.Register(
        nameof(MoreVisible),
        typeof(bool),
        typeof(MonthDayCell),
        new PropertyMetadata(false, OnVisualChanged));

    public static readonly DependencyProperty DateProperty = DependencyProperty.Register(
        nameof(Date),
        typeof(DateOnly),
        typeof(MonthDayCell),
        new PropertyMetadata(default(DateOnly)));

    public static readonly DependencyProperty EventCountProperty = DependencyProperty.Register(
        nameof(EventCount),
        typeof(int),
        typeof(MonthDayCell),
        new PropertyMetadata(0, OnVisualChanged));

    public MonthDayCell()
    {
        InitializeComponent();

        this.PointerEntered += (_, _) => HoverFill.Visibility = Visibility.Visible;
        this.PointerExited += (_, _) => HoverFill.Visibility = Visibility.Collapsed;
        this.Tapped += OnTapped;
        this.DoubleTapped += OnDoubleTapped;
        this.AllowDrop = true;
        this.DragOver += OnDragOver;
        this.Drop += OnDrop;
        this.GotFocus += (_, _) => FocusRing.Visibility = Visibility.Visible;
        this.LostFocus += (_, _) => FocusRing.Visibility = Visibility.Collapsed;
        this.ContextFlyout = BuildContextFlyout();
    }

    private bool _suppressVisualUpdates;

    /// <summary>
    /// Applies a month day in a single visual pass. Setting the individual
    /// dependency properties one by one would otherwise refresh the whole cell
    /// for every property.
    /// </summary>
    public void Apply(MonthDay day)
    {
        _suppressVisualUpdates = true;

        try
        {
            DayNumber = day.DayNumber.ToString();
            IsToday = day.IsToday;
            IsCurrentMonth = day.IsCurrentMonth;
            IsWeekend = day.IsWeekend;
            IsSelected = day.IsSelected;
            MoreLabel = day.MoreLabel;
            MoreVisible = day.MoreCount > 0;
            EventCount = day.EventCount;
            Date = day.Date;
        }
        finally
        {
            _suppressVisualUpdates = false;
        }

        UpdateVisuals();
        SetChips(day.Chips);
        Items = day.AllItems;
    }

    private MenuFlyout BuildContextFlyout()
    {
        var flyout = new MenuFlyout();

        var newEvent = new MenuFlyoutItem { Text = "New event", Icon = new SymbolIcon(Symbol.Add) };
        newEvent.Click += (_, _) => NewEventRequested?.Invoke(this, Date);
        flyout.Items.Add(newEvent);

        var newReminder = new MenuFlyoutItem { Text = "New reminder" };
        newReminder.Click += (_, _) => NewReminderRequested?.Invoke(this, Date);
        flyout.Items.Add(newReminder);

        return flyout;
    }

    public string DayNumber
    {
        get => (string)GetValue(DayNumberProperty);
        set => SetValue(DayNumberProperty, value);
    }

    public bool IsToday
    {
        get => (bool)GetValue(IsTodayProperty);
        set => SetValue(IsTodayProperty, value);
    }

    public bool IsCurrentMonth
    {
        get => (bool)GetValue(IsCurrentMonthProperty);
        set => SetValue(IsCurrentMonthProperty, value);
    }

    public bool IsWeekend
    {
        get => (bool)GetValue(IsWeekendProperty);
        set => SetValue(IsWeekendProperty, value);
    }

    public bool IsSelected
    {
        get => (bool)GetValue(IsSelectedProperty);
        set => SetValue(IsSelectedProperty, value);
    }

    public string MoreLabel
    {
        get => (string)GetValue(MoreLabelProperty);
        set => SetValue(MoreLabelProperty, value);
    }

    public bool MoreVisible
    {
        get => (bool)GetValue(MoreVisibleProperty);
        set => SetValue(MoreVisibleProperty, value);
    }

    public DateOnly Date
    {
        get => (DateOnly)GetValue(DateProperty);
        set => SetValue(DateProperty, value);
    }

    public int EventCount
    {
        get => (int)GetValue(EventCountProperty);
        set => SetValue(EventCountProperty, value);
    }

    public IReadOnlyList<MonthChip> Chips { get; set; } = Array.Empty<MonthChip>();

    public IReadOnlyList<CalendarItem> Items { get; set; } = Array.Empty<CalendarItem>();

    public event EventHandler<DateOnly>? DaySelected;

    public event EventHandler<DateOnly>? NewEventRequested;

    public event EventHandler<DateOnly>? NewReminderRequested;

    public event EventHandler<MonthChipDrop>? ChipDropped;

    private static void OnVisualChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        if (sender is MonthDayCell cell && !cell._suppressVisualUpdates)
        {
            cell.UpdateVisuals();
        }
    }

    private void UpdateVisuals()
    {
        DayText.Text = DayNumber;
        TodayText.Text = DayNumber;
        MoreText.Text = MoreLabel;

        TodayCircle.Visibility = IsToday ? Visibility.Visible : Visibility.Collapsed;
        DayText.Visibility = IsToday ? Visibility.Collapsed : Visibility.Visible;
        TodayText.Visibility = IsToday ? Visibility.Visible : Visibility.Collapsed;
        SelectionBorder.Visibility = IsSelected ? Visibility.Visible : Visibility.Collapsed;
        WeekendTint.Visibility = IsWeekend ? Visibility.Visible : Visibility.Collapsed;
        MoreButton.Visibility = MoreVisible ? Visibility.Visible : Visibility.Collapsed;

        HeatDot1.Visibility = EventCount >= 1 ? Visibility.Visible : Visibility.Collapsed;
        HeatDot2.Visibility = EventCount >= 3 ? Visibility.Visible : Visibility.Collapsed;
        HeatDot3.Visibility = EventCount >= 6 ? Visibility.Visible : Visibility.Collapsed;

        var opacity = IsCurrentMonth ? 1d : 0.4d;
        DayText.Opacity = opacity;
        TodayText.Opacity = opacity;
        TodayCircle.Opacity = opacity;
        HeatDots.Opacity = opacity;
    }

    public void SetChips(IReadOnlyList<MonthChip> chips)
    {
        Chips = chips;
        ChipsHost.Children.Clear();

        foreach (var chip in chips)
        {
            var view = new MonthChipView
            {
                Title = chip.Title,
                Color = chip.Color,
                ItemId = chip.Id,
            };

            view.DragStarting += (_, args) =>
            {
                if (args.Data is not null)
                {
                    args.Data.SetText(chip.Id);
                    args.Data.RequestedOperation = DataPackageOperation.Move;
                }
            };

            ChipsHost.Children.Add(view);
        }
    }

    private void OnTapped(object sender, TappedRoutedEventArgs e)
    {
        DaySelected?.Invoke(this, Date);
    }

    private void OnDoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        NewEventRequested?.Invoke(this, Date);
    }

    private void OnMoreClick(object sender, RoutedEventArgs e)
    {
        var list = new ListView
        {
            ItemsSource = Items,
            SelectionMode = ListViewSelectionMode.None,
            MaxHeight = 320,
        };

        var flyout = new Flyout
        {
            Content = list,
            Placement = FlyoutPlacementMode.BottomEdgeAlignedLeft,
        };

        flyout.ShowAt(MoreButton);
    }

    private void OnDragOver(object sender, DragEventArgs e)
    {
        e.AcceptedOperation = DataPackageOperation.Move;
        e.DragUIOverride.Caption = "Move";
    }

    private async void OnDrop(object sender, DragEventArgs e)
    {
        if (e.Data is not null)
        {
            var id = await e.Data.GetView().GetTextAsync();
            ChipDropped?.Invoke(this, new MonthChipDrop(id, Date));
        }
    }
}

public sealed record MonthChipDrop(string ItemId, DateOnly TargetDate);
