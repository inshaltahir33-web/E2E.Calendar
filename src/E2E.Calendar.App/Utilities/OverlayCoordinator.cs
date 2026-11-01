using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;

namespace E2E.Calendar.App.Utilities;

/// <summary>
/// The one open/close route for every transient surface (event drawer, details card
/// flyout, dialogs). It guarantees a single drawer or flyout open at a time, applies the
/// shared 150 ms ease-out open and 100 ms close, and lets Esc close the top-most surface.
/// </summary>
internal static class OverlayCoordinator
{
    public const int OpenDurationMs = Motion.OverlayOpenMs;

    public const int CloseDurationMs = Motion.OverlayCloseMs;

    private static readonly List<FlyoutBase> OpenFlyouts = new();
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<FlyoutBase, object> AnimatedFlyouts = new();
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<ContentDialog, object> AnimatedDialogs = new();
    private static Func<bool>? _isDrawerOpen;
    private static Func<Task>? _closeDrawer;
    private static Func<Task>? _dismissDrawer;

    /// <summary>
    /// MainWindow registers the drawer so flyouts can dismiss it and Esc can close it.
    /// <paramref name="close"/> is the user-initiated close (it may confirm discarding edits),
    /// <paramref name="dismiss"/> is the automatic close used when another surface opens and
    /// must never throw away unsaved edits.
    /// </summary>
    public static void RegisterDrawer(Func<bool> isOpen, Func<Task> close, Func<Task>? dismiss = null)
    {
        _isDrawerOpen = isOpen;
        _closeDrawer = close;
        _dismissDrawer = dismiss;
    }

    public static bool HasOpenSurface => OpenFlyouts.Count > 0 || IsDrawerOpen;

    private static bool IsDrawerOpen => _isDrawerOpen?.Invoke() == true;

    /// <summary>Closes every open flyout, optionally keeping the one that is about to open.</summary>
    public static void CloseFlyouts(FlyoutBase? keepOpen = null)
    {
        foreach (var flyout in OpenFlyouts.ToArray())
        {
            if (!ReferenceEquals(flyout, keepOpen))
            {
                flyout.Hide();
            }
        }
    }

    /// <summary>Steps the drawer aside for another surface without ever dropping unsaved edits.</summary>
    public static void CloseDrawer()
    {
        if (!IsDrawerOpen)
        {
            return;
        }

        var dismiss = _dismissDrawer ?? _closeDrawer;

        if (dismiss is not null)
        {
            _ = dismiss();
        }
    }

    /// <summary>
    /// Raises a flyout through the shared route: older flyouts close first, the drawer steps
    /// aside, and a content flyout plays the shared open/close motion. Menus keep their
    /// native popup transition but still obey the single-surface rule.
    /// </summary>
    public static void ShowFlyout(FlyoutBase flyout, FrameworkElement target, FlyoutShowOptions? options = null)
    {
        CloseFlyouts(flyout);
        CloseDrawer();

        Track(flyout);

        if (flyout is Flyout { Content: FrameworkElement content } && AnimatedFlyouts.TryAdd(flyout, flyout))
        {
            // Only one animation runs: ours. The platform's own open/close animation for this
            // flyout is disabled so open/close never double-animates.
            flyout.AreOpenCloseAnimationsEnabled = false;

            flyout.Opened += (_, _) => _ = Motion.FadeInAsync(content, OpenDurationMs);

            var closing = false;
            flyout.Closing += async (_, args) =>
            {
                if (closing || !Motion.IsEnabled)
                {
                    return;
                }

                closing = true;
                args.Cancel = true;

                try
                {
                    await Motion.FadeOutAsync(content, CloseDurationMs);
                }
                finally
                {
                    // Detach implicitly through the flag: Hide raises Closing again, which
                    // returns immediately because closing is still true.
                    try
                    {
                        flyout.Hide();
                    }
                    catch
                    {
                        // Already dismissed (light dismiss beat us to it).
                    }

                    closing = false;
                }
            };
        }

        if (options is null)
        {
            flyout.ShowAt(target);
        }
        else
        {
            flyout.ShowAt(target, options);
        }
    }

    /// <summary>
    /// Applies the shared dialog motion. Opening fades the content in over 150 ms; closing
    /// fades it out over 100 ms while the platform closes the dialog itself, so button
    /// results are never delayed or cancelled.
    /// </summary>
    public static void AnimateDialog(ContentDialog dialog)
    {
        // A dialog instance (e.g. the DI-registered paywall) can be shown more than once;
        // subscribe once so open/close never stack several fades.
        if (!AnimatedDialogs.TryAdd(dialog, dialog))
        {
            return;
        }

        dialog.Opened += (_, _) =>
        {
            if (dialog.Content is FrameworkElement content)
            {
                _ = Motion.FadeInAsync(content, OpenDurationMs);
            }
        };

        dialog.Closing += (_, _) =>
        {
            if (dialog.Content is FrameworkElement content)
            {
                _ = Motion.FadeOutAsync(content, CloseDurationMs);
            }
        };
    }

    /// <summary>Esc: the surface that was opened last wins. Returns true when something closed.</summary>
    public static bool TryCloseTopMost()
    {
        if (OpenFlyouts.Count > 0)
        {
            OpenFlyouts[^1].Hide();
            return true;
        }

        if (IsDrawerOpen && _closeDrawer is not null)
        {
            _ = _closeDrawer();
            return true;
        }

        return false;
    }

    private static void Track(FlyoutBase flyout)
    {
        if (OpenFlyouts.Contains(flyout))
        {
            return;
        }

        OpenFlyouts.Add(flyout);

        void OnClosed(object? sender, object args)
        {
            flyout.Closed -= OnClosed;
            OpenFlyouts.Remove(flyout);
        }

        flyout.Closed += OnClosed;
    }
}
