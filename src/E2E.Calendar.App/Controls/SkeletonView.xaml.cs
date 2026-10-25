using E2E.Calendar.App.Utilities;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;

namespace E2E.Calendar.App.Controls;

/// <summary>
/// Placeholder shown while a range query runs. The bars pulse so the user
/// knows the app is busy; the pulse stops as soon as the content is ready.
/// </summary>
public sealed partial class SkeletonView : UserControl
{
    public static readonly DependencyProperty RowsProperty = DependencyProperty.Register(
        nameof(Rows),
        typeof(int),
        typeof(SkeletonView),
        new PropertyMetadata(6, OnRowsChanged));

    public static readonly DependencyProperty IsActiveProperty = DependencyProperty.Register(
        nameof(IsActive),
        typeof(bool),
        typeof(SkeletonView),
        new PropertyMetadata(false, OnIsActiveChanged));

    public SkeletonView()
    {
        InitializeComponent();

        AutomationProperties.SetName(this, "Loading");
    }

    public int Rows
    {
        get => (int)GetValue(RowsProperty);
        set => SetValue(RowsProperty, value);
    }

    public bool IsActive
    {
        get => (bool)GetValue(IsActiveProperty);
        set => SetValue(IsActiveProperty, value);
    }

    private static void OnRowsChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        if (sender is SkeletonView view)
        {
            view.Build();
        }
    }

    private static void OnIsActiveChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        if (sender is SkeletonView view)
        {
            view.ApplyActive();
        }
    }

    private void Build()
    {
        BarsHost.Children.Clear();

        var count = Math.Max(1, Rows);
        var brush = ThemeBrushes.Get(this, "App.HoverBrush");

        for (var index = 0; index < count; index++)
        {
            var bar = new Border
            {
                Height = 16,
                CornerRadius = new CornerRadius(4),
                Background = brush,
                Opacity = 0.85 - (index % 3) * 0.2,
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };

            bar.Margin = new Thickness(0, 0, 24 + (index % 4) * 40, 0);

            BarsHost.Children.Add(bar);
        }

        ApplyActive();
    }

    private void ApplyActive()
    {
        Visibility = IsActive ? Visibility.Visible : Visibility.Collapsed;

        foreach (var child in BarsHost.Children)
        {
            if (IsActive)
            {
                Motion.Pulse(child);
            }
            else
            {
                ElementCompositionPreview.GetElementVisual(child).StopAnimation("Opacity");
            }
        }
    }
}
