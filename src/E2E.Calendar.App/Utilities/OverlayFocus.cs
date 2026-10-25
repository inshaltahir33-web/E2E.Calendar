using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;

namespace E2E.Calendar.App.Utilities;

/// <summary>
/// Focus bookkeeping shared by every overlay surface (dialogs, flyouts, menus).
/// WinUI does not reliably move focus back to the element that opened an overlay,
/// so callers capture the opener before showing and restore it once the overlay closes.
/// </summary>
internal static class OverlayFocus
{
    /// <summary>Returns the element that currently holds focus inside <paramref name="xamlRoot"/>.</summary>
    public static DependencyObject? Capture(XamlRoot? xamlRoot) =>
        xamlRoot is null ? null : FocusManager.GetFocusedElement(xamlRoot) as DependencyObject;

    /// <summary>Moves focus back to <paramref name="element"/> once the overlay has finished closing.</summary>
    public static void Restore(DependencyObject? element)
    {
        if (element is not FrameworkElement { IsLoaded: true } target)
        {
            return;
        }

        // Defer: the overlay's own teardown runs a focus pass after the close event,
        // which would otherwise overwrite the focus we set here.
        var queue = target.DispatcherQueue;

        if (queue is null)
        {
            _ = FocusManager.TryFocusAsync(target, FocusState.Programmatic);
            return;
        }

        queue.TryEnqueue(() =>
        {
            if (target.IsLoaded)
            {
                _ = FocusManager.TryFocusAsync(target, FocusState.Programmatic);
            }
        });
    }

    /// <summary>Shows a flyout and returns focus to the control that opened it when it closes.</summary>
    public static void ShowAt(
        FlyoutBase flyout,
        FrameworkElement target,
        FlyoutShowOptions? options = null,
        DependencyObject? opener = null)
    {
        ArgumentNullException.ThrowIfNull(flyout);
        ArgumentNullException.ThrowIfNull(target);

        var returnTo = opener ?? Capture(target.XamlRoot) ?? target;

        void OnClosed(object? sender, object args)
        {
            flyout.Closed -= OnClosed;
            Restore(returnTo);
        }

        flyout.Closed += OnClosed;

        if (options is null)
        {
            flyout.ShowAt(target);
        }
        else
        {
            flyout.ShowAt(target, options);
        }
    }
}
