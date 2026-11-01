using E2E.Calendar.App.Views;
using E2E.Calendar.Core.Abstractions;
using E2E.Calendar.Core.Models;
using E2E.Calendar.Core.Services;
using Microsoft.UI.Xaml;

namespace E2E.Calendar.App.Services;

/// <summary>
/// The delete entry point shared by the calendar grid and the details card: asks the scope
/// for recurring events, runs the core deletion, and keeps the last deletion for Undo.
/// </summary>
public sealed class EventDeletionCoordinator
{
    private readonly IEventRepository _events;
    private readonly EventDeletionService _deletions;
    private readonly IDialogService _dialogs;
    private EventDeletion? _pending;

    public EventDeletionCoordinator(
        IEventRepository events,
        EventDeletionService deletions,
        IDialogService dialogs)
    {
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(deletions);
        ArgumentNullException.ThrowIfNull(dialogs);

        _events = events;
        _deletions = deletions;
        _dialogs = dialogs;
    }

    public event EventHandler? Deleted;

    public async Task<bool> DeleteAsync(
        CalendarItem item,
        XamlRoot? xamlRoot,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);

        var row = await _events.GetByIdAsync(item.Id, cancellationToken);
        if (row is null)
        {
            return false;
        }

        RecurrenceEditScope? scope = null;

        if (row.RecurrenceMasterId is not null || !string.IsNullOrWhiteSpace(row.RRule))
        {
            scope = await RecurrenceEditDialog.ShowDeleteAsync(_dialogs, xamlRoot);

            if (scope is null)
            {
                return false;
            }
        }

        _pending = await _deletions.DeleteAsync(
            row,
            row.RecurrenceIdUtc ?? item.StartUtc,
            scope ?? RecurrenceEditScope.AllEvents,
            cancellationToken);

        Deleted?.Invoke(this, EventArgs.Empty);

        return true;
    }

    public void ForgetPending() => _pending = null;

    public async Task<bool> TryUndoAsync(CancellationToken cancellationToken = default)
    {
        var pending = _pending;
        if (pending is null)
        {
            return false;
        }

        _pending = null;

        try
        {
            await _deletions.UndoAsync(pending, cancellationToken);
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }
}
