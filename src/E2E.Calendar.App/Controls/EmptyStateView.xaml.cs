using E2E.Calendar.App.Utilities;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace E2E.Calendar.App.Controls;

public sealed partial class EmptyStateView : UserControl
{
    public static readonly DependencyProperty GlyphProperty = DependencyProperty.Register(
        nameof(Glyph),
        typeof(string),
        typeof(EmptyStateView),
        new PropertyMetadata("\uE787", OnContentChanged));

    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title),
        typeof(string),
        typeof(EmptyStateView),
        new PropertyMetadata(string.Empty, OnContentChanged));

    public static readonly DependencyProperty DescriptionProperty = DependencyProperty.Register(
        nameof(Description),
        typeof(string),
        typeof(EmptyStateView),
        new PropertyMetadata(string.Empty, OnContentChanged));

    public static readonly DependencyProperty ActionTextProperty = DependencyProperty.Register(
        nameof(ActionText),
        typeof(string),
        typeof(EmptyStateView),
        new PropertyMetadata(string.Empty, OnContentChanged));

    public EmptyStateView()
    {
        InitializeComponent();

        AutomationProperties.SetName(this, "Empty state");
    }

    public string Glyph
    {
        get => (string)GetValue(GlyphProperty);
        set => SetValue(GlyphProperty, value);
    }

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string Description
    {
        get => (string)GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    public string ActionText
    {
        get => (string)GetValue(ActionTextProperty);
        set => SetValue(ActionTextProperty, value);
    }

    public event EventHandler? ActionInvoked;

    private static void OnContentChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        if (sender is EmptyStateView view)
        {
            view.ApplyContent();
        }
    }

    private void ApplyContent()
    {
        GlyphIcon.Glyph = string.IsNullOrEmpty(Glyph) ? "\uE787" : Glyph;
        TitleText.Text = Title;
        DescriptionText.Text = Description;

        var hasDescription = !string.IsNullOrWhiteSpace(Description);
        DescriptionText.Visibility = hasDescription ? Visibility.Visible : Visibility.Collapsed;

        var hasAction = !string.IsNullOrWhiteSpace(ActionText);
        ActionButton.Content = ActionText;
        ActionButton.Visibility = hasAction ? Visibility.Visible : Visibility.Collapsed;

        AutomationProperties.SetName(this, string.IsNullOrWhiteSpace(Title) ? "Empty state" : Title);
    }

    private void OnActionClick(object sender, RoutedEventArgs e)
    {
        ActionInvoked?.Invoke(this, EventArgs.Empty);
    }
}
