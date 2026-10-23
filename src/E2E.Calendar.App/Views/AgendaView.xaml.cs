using E2E.Calendar.App.Utilities;
using E2E.Calendar.Core;
using E2E.Calendar.App.ViewModels;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Input;
using System.Numerics;
using Windows.Foundation;

namespace E2E.Calendar.App.Views;

public partial class AgendaView : Page
{
    private readonly EventEditorViewModel _editor;
    private readonly List<DayHeaderAnchor> _anchors = new();

    public AgendaView(AgendaViewModel viewModel, EventEditorViewModel editor)
    {
        ViewModel = viewModel;
        _editor = editor;

        InitializeComponent();

        ViewModel.PeriodChanged += OnPeriodChanged;
        ViewModel.PropertyChanged += OnViewModelPropertyChanged;
        EmptyStateView.ActionInvoked += OnEmptyStateAction;

        this.Loaded += OnLoaded;
        this.Unloaded += OnUnloaded;
    }

    public AgendaViewModel ViewModel { get; }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await ViewModel.RefreshAsync();

        UpdateEmptyState();
        UpdateStickyHeader();
    }

    private async void OnPeriodChanged(object? sender, EventArgs e) =>
        await Motion.SlideAndFadeInAsync(AgendaScroll);

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AgendaViewModel.IsLoading))
        {
            Skeleton.IsActive = ViewModel.IsLoading;
        }
        else if (e.PropertyName == nameof(AgendaViewModel.IsEmpty))
        {
            UpdateEmptyState();
        }
        else if (e.PropertyName == nameof(AgendaViewModel.Days))
        {
            _anchors.Clear();
            UpdateStickyHeader();
        }
    }

    private async void OnEmptyStateAction(object? sender, EventArgs e)
    {
        var timeZone = TimeZoneInfo.Local;
        var today = DateOnly.FromDateTime(DateTime.Now);
        var startUtc = timeZone.ToUtc(today.ToDateTime(new TimeOnly(9, 0)));

        await _editor.OpenNewAsync(startUtc, startUtc.AddHours(1));
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _anchors.Clear();
    }

    private async void OnViewChanged(object sender, ScrollViewerViewChangedEventArgs args)
    {
        if (args.IsIntermediate)
        {
            return;
        }

        UpdateStickyHeader();

        if (ViewModel.HasLater)
        {
            var extent = AgendaScroll.ExtentHeight;
            var viewport = AgendaScroll.ViewportHeight;

            if (extent - (AgendaScroll.VerticalOffset + viewport) < viewport)
            {
                await ViewModel.LoadLaterAsync();
            }
        }
    }

    private void OnDayElementPrepared(ItemsRepeater sender, ItemsRepeaterElementPreparedEventArgs args)
    {
        if (args.Element is not FrameworkElement element)
        {
            return;
        }

        if (element.FindName("AgendaDayHeader") is not FrameworkElement header)
        {
            return;
        }

        var day = element.DataContext as AgendaDayViewModel;

        if (day is null)
        {
            return;
        }

        _anchors.RemoveAll(anchor => ReferenceEquals(anchor.Day, day));
        _anchors.Add(new DayHeaderAnchor(day, header));
    }

    private void UpdateStickyHeader()
    {
        if (_anchors.Count == 0)
        {
            StickyHeaderBar.Visibility = Visibility.Collapsed;
            return;
        }

        var offset = AgendaScroll.VerticalOffset;
        AgendaDayViewModel? current = null;

        foreach (var anchor in _anchors)
        {
            var y = anchor.Header.TransformToVisual(AgendaScroll).TransformPoint(new Point(0, 0)).Y;

            if (y - offset <= 1)
            {
                current = anchor.Day;
            }
        }

        current ??= _anchors[0].Day;

        ViewModel.StickyHeader = current.IsToday
            ? $"Today \u00B7 {current.DayText} \u00B7 {current.CountLabel}"
            : $"{current.DayText} \u00B7 {current.CountLabel}";

        var showSticky = AgendaScroll.VerticalOffset > 2;

        StickyHeaderBar.Visibility = showSticky ? Visibility.Visible : Visibility.Collapsed;
    }

    private async void OnItemCheckBoxClick(object sender, RoutedEventArgs e)
    {
        if (sender is not CheckBox checkBox || checkBox.Tag is not AgendaItemViewModel item)
        {
            return;
        }

        await ViewModel.ToggleCompletedAsync(item);

        AnimateCompletion(item, checkBox);
    }

    private static void AnimateCompletion(AgendaItemViewModel item, UIElement target)
    {
        var visual = ElementCompositionPreview.GetElementVisual(target);
        var compositor = visual.Compositor;
        var animator = compositor.CreateVector3KeyFrameAnimation();
        var offset = new Vector3(0, -4, 0);
        var easing = CompositionEasingFunction.CreateCubicBezierEasingFunction(
            compositor,
            new Vector2(0.1f, 0.9f),
            new Vector2(0.2f, 1f));

        animator.InsertKeyFrame(0f, offset);
        animator.InsertKeyFrame(1f, Vector3.Zero, easing);
        animator.Duration = TimeSpan.FromMilliseconds(160);

        visual.StartAnimation("Offset", animator);
    }

    private void OnRowPointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (sender is FrameworkElement element)
        {
            element.Opacity = 1;
        }
    }

    private void OnRowPointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (sender is FrameworkElement element)
        {
            element.Opacity = 0;
        }
    }

    private void UpdateEmptyState()
    {
        EmptyStateView.Visibility = ViewModel.IsEmpty ? Visibility.Visible : Visibility.Collapsed;
    }

    private async void OnLoadEarlierClick(object sender, RoutedEventArgs e)
    {
        await ViewModel.LoadEarlierAsync();

        UpdateStickyHeader();
    }

    private async void OnLoadLaterClick(object sender, RoutedEventArgs e)
    {
        await ViewModel.LoadLaterAsync();

        UpdateStickyHeader();
    }

    private sealed record DayHeaderAnchor(AgendaDayViewModel Day, FrameworkElement Header);
}
