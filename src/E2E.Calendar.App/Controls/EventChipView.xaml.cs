using System.Numerics;
using E2E.Calendar.App.Utilities;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;

namespace E2E.Calendar.App.Controls;

public sealed partial class EventChipView : UserControl
{
    private readonly ThemeShadow _shadow = new();

    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title),
        typeof(string),
        typeof(EventChipView),
        new PropertyMetadata(string.Empty, OnVisualChanged));

    public static readonly DependencyProperty TimeTextProperty = DependencyProperty.Register(
        nameof(TimeText),
        typeof(string),
        typeof(EventChipView),
        new PropertyMetadata(string.Empty, OnVisualChanged));

    public static readonly DependencyProperty ColorHexProperty = DependencyProperty.Register(
        nameof(ColorHex),
        typeof(string),
        typeof(EventChipView),
        new PropertyMetadata(null, OnVisualChanged));

    public static readonly DependencyProperty IsSelectedProperty = DependencyProperty.Register(
        nameof(IsSelected),
        typeof(bool),
        typeof(EventChipView),
        new PropertyMetadata(false, OnVisualChanged));

    public static readonly DependencyProperty IsDoneProperty = DependencyProperty.Register(
        nameof(IsDone),
        typeof(bool),
        typeof(EventChipView),
        new PropertyMetadata(false, OnVisualChanged));

    public static readonly DependencyProperty IsTickableProperty = DependencyProperty.Register(
        nameof(IsTickable),
        typeof(bool),
        typeof(EventChipView),
        new PropertyMetadata(false, OnVisualChanged));

    public static readonly DependencyProperty ShowTimeProperty = DependencyProperty.Register(
        nameof(ShowTime),
        typeof(bool),
        typeof(EventChipView),
        new PropertyMetadata(true, OnVisualChanged));

    public static readonly DependencyProperty TooltipTextProperty = DependencyProperty.Register(
        nameof(TooltipText),
        typeof(string),
        typeof(EventChipView),
        new PropertyMetadata(null, OnVisualChanged));

    private bool _isHovered;
    private bool _isPressed;
    private bool _visualDirty = true;

    public EventChipView()
    {
        InitializeComponent();

        ChipBorder.Shadow = _shadow;

        PointerEntered += OnPointerEntered;
        PointerExited += OnPointerExited;
        PointerPressed += OnPointerPressed;
        GotFocus += OnGotFocus;
        LostFocus += OnLostFocus;
        Loaded += OnLoaded;
        CheckBox.Tapped += OnCheckBoxTapped;
        ActualThemeChanged += (_, _) => ApplyVisual();
    }

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string TimeText
    {
        get => (string)GetValue(TimeTextProperty);
        set => SetValue(TimeTextProperty, value);
    }

    public string? ColorHex
    {
        get => (string?)GetValue(ColorHexProperty);
        set => SetValue(ColorHexProperty, value);
    }

    public bool IsSelected
    {
        get => (bool)GetValue(IsSelectedProperty);
        set => SetValue(IsSelectedProperty, value);
    }

    public bool IsDone
    {
        get => (bool)GetValue(IsDoneProperty);
        set => SetValue(IsDoneProperty, value);
    }

    public bool IsTickable
    {
        get => (bool)GetValue(IsTickableProperty);
        set => SetValue(IsTickableProperty, value);
    }

    public bool ShowTime
    {
        get => (bool)GetValue(ShowTimeProperty);
        set => SetValue(ShowTimeProperty, value);
    }

    public string? TooltipText
    {
        get => (string?)GetValue(TooltipTextProperty);
        set => SetValue(TooltipTextProperty, value);
    }

    public event EventHandler? CheckToggled;

    private static void OnVisualChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        if (sender is EventChipView chip)
        {
            chip.RequestApply();
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_visualDirty)
        {
            _visualDirty = false;
            ApplyVisual();
        }
    }

    /// <summary>
    /// Chips are configured before they join the tree, so the visual pass is
    /// deferred until all properties are set instead of running once per property.
    /// </summary>
    private void RequestApply()
    {
        if (!IsLoaded)
        {
            _visualDirty = true;
            return;
        }

        _visualDirty = false;
        ApplyVisual();
    }

    private void OnPointerEntered(object sender, PointerRoutedEventArgs args)
    {
        _isHovered = true;
        ApplyVisual();
    }

    private void OnPointerExited(object sender, PointerRoutedEventArgs args)
    {
        _isHovered = false;
        _isPressed = false;
        ApplyVisual();
    }

    private void OnPointerPressed(object sender, PointerRoutedEventArgs args)
    {
        _isPressed = true;
        ApplyVisual();
    }

    private void OnGotFocus(object sender, RoutedEventArgs e)
    {
        FocusRing.Visibility = Visibility.Visible;
    }

    private void OnLostFocus(object sender, RoutedEventArgs e)
    {
        FocusRing.Visibility = Visibility.Collapsed;
    }
    private void OnCheckBoxTapped(object sender, TappedRoutedEventArgs args)
    {
        CheckToggled?.Invoke(this, EventArgs.Empty);
    }

    private void ApplyVisual()
    {
        var isTickable = IsTickable;

        TitleText.Text = Title;
        TimeTextBlock.Text = TimeText;
        TimeTextBlock.Visibility = ShowTime && !string.IsNullOrEmpty(TimeText)
            ? Visibility.Visible
            : Visibility.Collapsed;

        CheckBox.Visibility = isTickable ? Visibility.Visible : Visibility.Collapsed;
        CheckGlyph.Visibility = IsDone ? Visibility.Visible : Visibility.Collapsed;
        CheckBox.Background = IsDone
            ? ThemeBrushes.Get(this, "App.AccentBrush")
            : ThemeBrushes.Get(this, "ControlFillColorTransparentBrush");
        CheckBox.BorderBrush = IsDone
            ? ThemeBrushes.Get(this, "App.AccentBrush")
            : ThemeBrushes.Get(this, "App.DividerStrongBrush");

        ColorBar.Visibility = isTickable ? Visibility.Collapsed : Visibility.Visible;
        var chipBrushes = EventColorService.GetChipBrushes(ColorHex, ActualTheme);
        ColorBar.Background = chipBrushes.Stripe;

        ChipBorder.Background = isTickable
            ? ThemeBrushes.Get(this, "ControlFillColorTransparentBrush")
            : chipBrushes.Fill;

        TitleText.Foreground = isTickable
            ? ThemeBrushes.Get(this, "App.TextPrimaryBrush")
            : chipBrushes.Text;
        TimeTextBlock.Foreground = isTickable
            ? ThemeBrushes.Get(this, "App.TextSecondaryBrush")
            : chipBrushes.Text;
        TimeTextBlock.Opacity = 0.8;

        if (IsSelected)
        {
            ChipBorder.BorderBrush = ThemeBrushes.Get(this, "App.AccentBrush");
            ChipBorder.BorderThickness = new Thickness(2);
        }
        else
        {
            ChipBorder.BorderBrush = ThemeBrushes.Get(this, _isHovered ? "App.DividerStrongBrush" : "App.DividerBrush");
            ChipBorder.BorderThickness = new Thickness(1);
        }

        ChipBorder.Translation = _isHovered ? new Vector3(0, 0, 16) : Vector3.Zero;
        ChipBorder.Opacity = _isPressed ? 0.85 : 1;

        Opacity = IsDone ? 0.55 : 1;

        if (!string.IsNullOrWhiteSpace(TooltipText))
        {
            ToolTipService.SetToolTip(this, TooltipText);
        }

        AutomationProperties.SetName(this, TooltipText ?? Title);
        AutomationProperties.SetItemStatus(this, TimeText);
    }
}
