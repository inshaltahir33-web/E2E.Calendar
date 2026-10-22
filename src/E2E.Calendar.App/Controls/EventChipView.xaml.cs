using E2E.Calendar.App.Utilities;
using E2E.Calendar.Core.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace E2E.Calendar.App.Controls;

public sealed partial class EventChipView : UserControl
{
    private const byte TintAlphaLight = 0x2E;
    private const byte TintAlphaDark = 0x47;
    private const byte HoverAlphaLight = 0x3F;
    private const byte HoverAlphaDark = 0x5C;

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

        PointerEntered += OnPointerEntered;
        PointerExited += OnPointerExited;
        PointerPressed += OnPointerPressed;
        GotFocus += OnGotFocus;
        LostFocus += OnLostFocus;
        Loaded += OnLoaded;
        CheckBox.Tapped += OnCheckBoxTapped;
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
        var isDark = ActualTheme == ElementTheme.Dark;
        var isTickable = IsTickable;

        TitleText.Text = Title;
        TimeTextBlock.Text = TimeText;
        TimeTextBlock.Visibility = ShowTime && !string.IsNullOrEmpty(TimeText)
            ? Visibility.Visible
            : Visibility.Collapsed;

        CheckBox.Visibility = isTickable ? Visibility.Visible : Visibility.Collapsed;
        CheckGlyph.Visibility = IsDone ? Visibility.Visible : Visibility.Collapsed;
        CheckBox.Background = IsDone
            ? (Brush)Application.Current.Resources["AccentFillColorDefaultBrush"]
            : (Brush)Application.Current.Resources["ControlFillColorTransparentBrush"];
        CheckBox.BorderBrush = IsDone
            ? (Brush)Application.Current.Resources["AccentFillColorDefaultBrush"]
            : (Brush)Application.Current.Resources["DividerStrokeColorDefaultBrush"];

        ColorBar.Visibility = isTickable ? Visibility.Collapsed : Visibility.Visible;

        var hasColor = ColorHelper.TryParse(ColorHex, out var color);

        ColorBar.Background = hasColor
            ? ColorHelper.FromColor(color)
            : (Brush)Application.Current.Resources["AccentFillColorDefaultBrush"];

        if (isTickable)
        {
            ChipBorder.Background = ColorHelper.Transparent;
        }
        else if (hasColor)
        {
            var alpha = _isPressed
                ? (isDark ? (byte)0x70 : (byte)0x52)
                : _isHovered
                    ? (isDark ? HoverAlphaDark : HoverAlphaLight)
                    : (isDark ? TintAlphaDark : TintAlphaLight);

            ChipBorder.Background = ColorHelper.FromColor(Color.FromArgb(alpha, color.R, color.G, color.B));
        }
        else
        {
            ChipBorder.Background = (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"];
        }

        if (IsSelected)
        {
            ChipBorder.BorderBrush = (Brush)Application.Current.Resources["AccentFillColorDefaultBrush"];
            ChipBorder.BorderThickness = new Thickness(2);
        }
        else
        {
            ChipBorder.BorderBrush = ColorHelper.FromColor(
                Color.FromArgb(isDark ? (byte)0x40 : (byte)0x26, color.R, color.G, color.B));
            ChipBorder.BorderThickness = new Thickness(1);
        }

        Opacity = IsDone ? 0.55 : 1;

        if (!string.IsNullOrWhiteSpace(TooltipText))
        {
            ToolTipService.SetToolTip(this, TooltipText);
        }

        AutomationProperties.SetName(this, TooltipText ?? Title);
    }
}
