using E2E.Calendar.App.Utilities;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.ApplicationModel.DataTransfer;

namespace E2E.Calendar.App.Controls;

public sealed partial class MonthChipView : UserControl
{
    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title),
        typeof(string),
        typeof(MonthChipView),
        new PropertyMetadata(string.Empty, OnVisualChanged));

    public static readonly DependencyProperty ColorProperty = DependencyProperty.Register(
        nameof(Color),
        typeof(string),
        typeof(MonthChipView),
        new PropertyMetadata(null, OnVisualChanged));

    public static readonly DependencyProperty ItemIdProperty = DependencyProperty.Register(
        nameof(ItemId),
        typeof(string),
        typeof(MonthChipView),
        new PropertyMetadata(string.Empty));

    public MonthChipView()
    {
        InitializeComponent();

        CanDrag = true;
        DragStarting += OnDragStarting;

        PointerEntered += (_, _) => ApplyHover(true);
        PointerExited += (_, _) => ApplyHover(false);
        GotFocus += (_, _) => FocusRing.Visibility = Visibility.Visible;
        LostFocus += (_, _) => FocusRing.Visibility = Visibility.Collapsed;
        Loaded += (_, _) => ApplyVisual();
        ActualThemeChanged += (_, _) => ApplyVisual();
    }

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string? Color
    {
        get => (string?)GetValue(ColorProperty);
        set => SetValue(ColorProperty, value);
    }

    public string ItemId
    {
        get => (string)GetValue(ItemIdProperty);
        set => SetValue(ItemIdProperty, value);
    }

    private static void OnVisualChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        if (sender is MonthChipView chip)
        {
            chip.ApplyVisual();
        }
    }

    private void ApplyVisual()
    {
        TitleText.Text = Title;
        var chipBrushes = EventColorService.GetChipBrushes(Color, ActualTheme);
        TitleText.Foreground = chipBrushes.Text;
        AccentEdge.Background = chipBrushes.Stripe;
        ChipBorder.Background = chipBrushes.Fill;

        AutomationProperties.SetName(this, Title);
        AutomationProperties.SetItemStatus(this, Color);
    }

    private void ApplyHover(bool hovered)
    {
        // The chip has no border, so hover feedback is a small press-in of the pastel fill.
        ChipBorder.Opacity = hovered ? 0.88 : 1d;
    }

    private void OnDragStarting(UIElement sender, DragStartingEventArgs args)
    {
        args.Data.SetText(ItemId);
        args.Data.RequestedOperation = DataPackageOperation.Move;
    }
}
