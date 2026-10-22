using E2E.Calendar.App.Utilities;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;

namespace E2E.Calendar.App.Controls;

public sealed partial class MonthChipView : UserControl
{
    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title),
        typeof(string),
        typeof(MonthChipView),
        new PropertyMetadata(string.Empty, OnTitleChanged));

    public static readonly DependencyProperty ColorProperty = DependencyProperty.Register(
        nameof(Color),
        typeof(string),
        typeof(MonthChipView),
        new PropertyMetadata(null, OnColorChanged));

    public static readonly DependencyProperty ItemIdProperty = DependencyProperty.Register(
        nameof(ItemId),
        typeof(string),
        typeof(MonthChipView),
        new PropertyMetadata(string.Empty));

    public MonthChipView()
    {
        InitializeComponent();

        this.CanDrag = true;
        this.DragStarting += OnDragStarting;

        PointerEntered += (_, _) => HoverFill.Visibility = Visibility.Visible;
        PointerExited += (_, _) => HoverFill.Visibility = Visibility.Collapsed;
        GotFocus += (_, _) => FocusRing.Visibility = Visibility.Visible;
        LostFocus += (_, _) => FocusRing.Visibility = Visibility.Collapsed;
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

    private static void OnTitleChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        if (sender is MonthChipView chip)
        {
            chip.TitleText.Text = chip.Title;
        }
    }

    private static void OnColorChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        if (sender is MonthChipView chip)
        {
            chip.AccentEdge.Background = ColorHelper.FromHex(chip.Color);
        }
    }

    private void OnDragStarting(UIElement sender, DragStartingEventArgs args)
    {
        args.Data.SetText(ItemId);
        args.Data.RequestedOperation = DataPackageOperation.Move;
    }
}
