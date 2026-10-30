using E2E.Calendar.App.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;

namespace E2E.Calendar.App.Controls;

public sealed partial class DayCell : UserControl
{
    public static readonly DependencyProperty DayNumberProperty = DependencyProperty.Register(
        nameof(DayNumber),
        typeof(string),
        typeof(DayCell),
        new PropertyMetadata(string.Empty, OnVisualChanged));

    public static readonly DependencyProperty IsTodayProperty = DependencyProperty.Register(
        nameof(IsToday),
        typeof(bool),
        typeof(DayCell),
        new PropertyMetadata(false, OnVisualChanged));

    public static readonly DependencyProperty IsSelectedProperty = DependencyProperty.Register(
        nameof(IsSelected),
        typeof(bool),
        typeof(DayCell),
        new PropertyMetadata(false, OnVisualChanged));

    public static readonly DependencyProperty IsInSelectedWeekProperty = DependencyProperty.Register(
        nameof(IsInSelectedWeek),
        typeof(bool),
        typeof(DayCell),
        new PropertyMetadata(false, OnVisualChanged));

    public static readonly DependencyProperty IsCurrentMonthProperty = DependencyProperty.Register(
        nameof(IsCurrentMonth),
        typeof(bool),
        typeof(DayCell),
        new PropertyMetadata(true, OnVisualChanged));

    public static readonly DependencyProperty HasEventsProperty = DependencyProperty.Register(
        nameof(HasEvents),
        typeof(bool),
        typeof(DayCell),
        new PropertyMetadata(false, OnVisualChanged));

    public static readonly DependencyProperty AutomationNameProperty = DependencyProperty.Register(
        nameof(AutomationName),
        typeof(string),
        typeof(DayCell),
        new PropertyMetadata(string.Empty, OnVisualChanged));

    public DayCell()
    {
        InitializeComponent();

        PointerEntered += (_, _) => HoverFill.Visibility = Visibility.Visible;
        PointerExited += (_, _) => HoverFill.Visibility = Visibility.Collapsed;
        PointerPressed += (_, _) => HoverFill.Visibility = Visibility.Collapsed;
        GotFocus += (_, _) => FocusRing.Visibility = Visibility.Visible;
        LostFocus += (_, _) => FocusRing.Visibility = Visibility.Collapsed;
    }

    private bool _suppressVisualUpdates;

    /// <summary>
    /// Sets every visual state in one pass so a day cell is not refreshed six
    /// times while it is being produced by the grid builder.
    /// </summary>
    public void Apply(MiniCalendarDayViewModel day)
    {
        _suppressVisualUpdates = true;

        try
        {
            DayNumber = day.Label;
            IsToday = day.IsToday;
            IsSelected = day.IsSelected;
            IsInSelectedWeek = day.IsInSelectedWeek;
            IsCurrentMonth = day.IsCurrentMonth;
            HasEvents = day.EventCount > 0;
            AutomationName = day.AutomationName;
            Tag = day;
        }
        finally
        {
            _suppressVisualUpdates = false;
        }

        UpdateVisuals();
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

    public bool IsSelected
    {
        get => (bool)GetValue(IsSelectedProperty);
        set => SetValue(IsSelectedProperty, value);
    }

    public bool IsInSelectedWeek
    {
        get => (bool)GetValue(IsInSelectedWeekProperty);
        set => SetValue(IsInSelectedWeekProperty, value);
    }

    public bool IsCurrentMonth
    {
        get => (bool)GetValue(IsCurrentMonthProperty);
        set => SetValue(IsCurrentMonthProperty, value);
    }

    public bool HasEvents
    {
        get => (bool)GetValue(HasEventsProperty);
        set => SetValue(HasEventsProperty, value);
    }

    public string AutomationName
    {
        get => (string)GetValue(AutomationNameProperty);
        set => SetValue(AutomationNameProperty, value);
    }

    private static void OnVisualChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        if (sender is DayCell cell && !cell._suppressVisualUpdates)
        {
            cell.UpdateVisuals();
        }
    }

    private void UpdateVisuals()
    {
        DayText.Text = DayNumber;
        TodayText.Text = DayNumber;

        // No selected-week background any more: the AccentSoft row is a hover affordance only.
        SelectedRing.Visibility = IsSelected && !IsToday ? Visibility.Visible : Visibility.Collapsed;
        TodayCircle.Visibility = IsToday ? Visibility.Visible : Visibility.Collapsed;
        TodayText.Visibility = IsToday ? Visibility.Visible : Visibility.Collapsed;
        DayText.Visibility = IsToday ? Visibility.Collapsed : Visibility.Visible;
        EventDot.Visibility = HasEvents ? Visibility.Visible : Visibility.Collapsed;

        DayText.Opacity = IsCurrentMonth ? 1d : 0.4d;
        TodayText.Opacity = IsCurrentMonth ? 1d : 0.4d;
        EventDot.Opacity = IsCurrentMonth ? 1d : 0.4d;

        AutomationProperties.SetName(this, AutomationName);
    }
}
