using E2E.Calendar.App.ViewModels;
using E2E.Calendar.Core.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;

namespace E2E.Calendar.App.Views;

public partial class AgendaView : Page
{
    private readonly List<DayAnchor> _anchors = new();

    public AgendaView(AgendaViewModel viewModel)
    {
        ViewModel = viewModel;

        InitializeComponent();

        DaysRepeater.ElementPrepared += OnElementPrepared;
    }

    public AgendaViewModel ViewModel { get; }

    private void OnElementPrepared(ItemsRepeater sender, ItemsRepeaterElementPreparedEventArgs args)
    {
        if (args.Element is not FrameworkElement element || args.Index >= ViewModel.Days.Count)
        {
            return;
        }

        var offset = element.TransformToVisual(DaysRepeater).TransformPoint(new Point(0, 0));
        _anchors.Add(new DayAnchor(offset.Y, ViewModel.Days[args.Index].Header));
    }

    private void OnViewChanged(object sender, ScrollViewerViewChangedEventArgs args)
    {
        var offset = ScrollViewer.VerticalOffset;

        var current = _anchors
            .Where(anchor => anchor.Top <= offset)
            .OrderByDescending(anchor => anchor.Top)
            .FirstOrDefault();

        PinnedHeader.Text = current?.Header ?? string.Empty;
        PinnedHeader.Visibility = string.IsNullOrEmpty(current?.Header) ? Visibility.Collapsed : Visibility.Visible;
    }

    private sealed record DayAnchor(double Top, string Header);
}
