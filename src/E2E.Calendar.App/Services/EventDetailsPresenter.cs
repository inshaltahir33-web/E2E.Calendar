using E2E.Calendar.App.Controls;
using E2E.Calendar.App.Utilities;
using E2E.Calendar.App.ViewModels;
using E2E.Calendar.Core.Abstractions;
using E2E.Calendar.Core.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;

namespace E2E.Calendar.App.Services;

/// <summary>
/// Shows the read-only event card for every calendar surface. Editing is still handled by the
/// one shared editor drawer, so the card only surfaces the details and the two actions.
/// </summary>
public sealed class EventDetailsPresenter
{
    private readonly IEventRepository _events;
    private readonly IEventService _eventService;
    private readonly EventEditorViewModel _editor;

    public EventDetailsPresenter(
        IEventRepository events,
        IEventService eventService,
        EventEditorViewModel editor)
    {
        _events = events;
        _eventService = eventService;
        _editor = editor;
    }

    public async Task ShowAsync(FrameworkElement anchor, CalendarItem item, Func<Task> refresh)
    {
        ArgumentNullException.ThrowIfNull(anchor);
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(refresh);

        // Reminders and tasks keep their checkbox behaviour and never grow an event card.
        if (item.IsTickable)
        {
            await _editor.OpenAsync(item.Id);
            return;
        }

        var details = await _events.GetByIdAsync(item.Id);

        var card = new EventDetailsCard();
        card.Populate(item, details);

        var flyout = new Flyout
        {
            Content = card,
            Placement = FlyoutPlacementMode.TopEdgeAlignedLeft,
        };

        card.EditRequested += async (_, _) =>
        {
            flyout.Hide();
            await _editor.OpenAsync(item.Id);
        };

        card.DeleteRequested += async (_, _) =>
        {
            flyout.Hide();
            await _eventService.SoftDeleteAsync(item.Id);
            await refresh();
        };

        OverlayFocus.ShowAt(flyout, anchor);
    }
}
