using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Hosting;
using System.Numerics;

namespace E2E.Calendar.App.Utilities;

/// <summary>
/// Central place for the app transitions. Every animation is skipped when the
/// user turned off animation effects in Windows (Settings > Accessibility >
/// Visual effects > Animation effects).
/// </summary>
public static class Motion
{
    public const int FastDurationMs = 150;
    public const int MediumDurationMs = 167;
    public const int SlowDurationMs = 250;

    public const double SlideOffsetPixels = 8;

    private static bool? _enabled;

    public static bool IsEnabled
    {
        get
        {
            _enabled ??= ReadAnimationsEnabled();
            return _enabled.Value;
        }
    }

    public static void Refresh() => _enabled = ReadAnimationsEnabled();

    private static bool ReadAnimationsEnabled()
    {
        try
        {
            var settings = new Windows.UI.ViewManagement.UISettings();
            return settings.AnimationsEnabled;
        }
        catch (Exception)
        {
            return true;
        }
    }

    public static async Task FadeInAsync(UIElement element, int durationMs = FastDurationMs)
    {
        if (element is null)
        {
            return;
        }

        if (!IsEnabled)
        {
            element.Opacity = 1;
            return;
        }

        // Set the target value before starting the composition animation — setting
        // a XAML property after StartAnimation would cancel the animation.
        element.Opacity = 1;

        var visual = ElementCompositionPreview.GetElementVisual(element);
        var animation = visual.Compositor.CreateScalarKeyFrameAnimation();

        animation.InsertKeyFrame(0f, 0f);
        animation.InsertKeyFrame(1f, 1f);
        animation.Duration = TimeSpan.FromMilliseconds(durationMs);

        visual.StartAnimation("Opacity", animation);

        await Task.Delay(durationMs);
    }

    public static async Task SlideAndFadeInAsync(
        UIElement element,
        double offsetPixels = SlideOffsetPixels,
        int durationMs = FastDurationMs)
    {
        if (element is null)
        {
            return;
        }

        if (!IsEnabled)
        {
            element.Opacity = 1;
            return;
        }

        element.Opacity = 1;

        var visual = ElementCompositionPreview.GetElementVisual(element);
        var compositor = visual.Compositor;
        var easing = CompositionEasingFunction.CreateCubicBezierEasingFunction(
            compositor,
            new Vector2(0.1f, 0.9f),
            new Vector2(0.2f, 1f));

        var opacity = compositor.CreateScalarKeyFrameAnimation();
        opacity.InsertKeyFrame(0f, 0f);
        opacity.InsertKeyFrame(1f, 1f);
        opacity.Duration = TimeSpan.FromMilliseconds(durationMs);

        var offset = compositor.CreateVector3KeyFrameAnimation();
        offset.InsertKeyFrame(0f, new Vector3(0, (float)offsetPixels, 0));
        offset.InsertKeyFrame(1f, Vector3.Zero, easing);
        offset.Duration = TimeSpan.FromMilliseconds(durationMs);

        visual.StartAnimation("Opacity", opacity);
        visual.StartAnimation("Offset", offset);

        await Task.Delay(durationMs);
    }

    /// <summary>
    /// Slides a drawer in horizontally from the right edge and fades it in.
    /// Only the composition translation (Offset X) and opacity are animated —
    /// layout (width, margins, columns) never changes, so nothing behind the
    /// drawer reflows. Used to open the event editor.
    /// </summary>
    public static async Task SlideInFromRightAsync(
        UIElement element,
        double offsetPixels,
        int durationMs = FastDurationMs)
    {
        if (element is null)
        {
            return;
        }

        if (!IsEnabled)
        {
            element.Opacity = 1;
            return;
        }

        element.Opacity = 1;

        var visual = ElementCompositionPreview.GetElementVisual(element);
        var compositor = visual.Compositor;
        var easing = CompositionEasingFunction.CreateCubicBezierEasingFunction(
            compositor,
            new Vector2(0.1f, 0.9f),
            new Vector2(0.2f, 1f));

        var opacity = compositor.CreateScalarKeyFrameAnimation();
        opacity.InsertKeyFrame(0f, 0f);
        opacity.InsertKeyFrame(1f, 1f, easing);
        opacity.Duration = TimeSpan.FromMilliseconds(durationMs);

        var offset = compositor.CreateVector3KeyFrameAnimation();
        offset.InsertKeyFrame(0f, new Vector3((float)offsetPixels, 0, 0));
        offset.InsertKeyFrame(1f, Vector3.Zero, easing);
        offset.Duration = TimeSpan.FromMilliseconds(durationMs);

        visual.StartAnimation("Opacity", opacity);
        visual.StartAnimation("Offset", offset);

        await Task.Delay(durationMs);
    }

    /// <summary>
    /// Slides a drawer out to the right edge while fading it. Same duration and
    /// easing as <see cref="SlideInFromRightAsync"/> so open/close feel symmetric.
    /// The caller hides the element only after the returned task completes, which
    /// removes the one-frame layout snap on close.
    /// </summary>
    public static async Task SlideOutToRightAsync(
        UIElement element,
        double offsetPixels,
        int durationMs = FastDurationMs)
    {
        if (element is null)
        {
            return;
        }

        if (!IsEnabled)
        {
            element.Opacity = 0;
            return;
        }

        var visual = ElementCompositionPreview.GetElementVisual(element);
        var compositor = visual.Compositor;
        var easing = CompositionEasingFunction.CreateCubicBezierEasingFunction(
            compositor,
            new Vector2(0.1f, 0.9f),
            new Vector2(0.2f, 1f));

        var opacity = compositor.CreateScalarKeyFrameAnimation();
        opacity.InsertKeyFrame(0f, 1f);
        opacity.InsertKeyFrame(1f, 0f, easing);
        opacity.Duration = TimeSpan.FromMilliseconds(durationMs);

        var offset = compositor.CreateVector3KeyFrameAnimation();
        offset.InsertKeyFrame(0f, Vector3.Zero);
        offset.InsertKeyFrame(1f, new Vector3((float)offsetPixels, 0, 0), easing);
        offset.Duration = TimeSpan.FromMilliseconds(durationMs);

        visual.StartAnimation("Opacity", opacity);
        visual.StartAnimation("Offset", offset);

        await Task.Delay(durationMs);
    }

    /// <summary>
    /// Slides an element in horizontally from the left and fades it in. Used for
    /// the sidebar when it collapses or expands; the element's layout has
    /// already been committed so this is a purely visual animation.
    /// </summary>
    public static void SlideInFromLeftAsync(
        UIElement element,
        int durationMs = MediumDurationMs,
        double offsetPixels = SlideOffsetPixels)
    {
        if (element is null || !IsEnabled)
        {
            if (element is not null)
            {
                element.Opacity = 1;
            }

            return;
        }

        element.Opacity = 1;

        var visual = ElementCompositionPreview.GetElementVisual(element);
        var compositor = visual.Compositor;
        var easing = CompositionEasingFunction.CreateCubicBezierEasingFunction(
            compositor,
            new Vector2(0.1f, 0.9f),
            new Vector2(0.2f, 1f));

        var opacity = compositor.CreateScalarKeyFrameAnimation();
        opacity.InsertKeyFrame(0f, 0f);
        opacity.InsertKeyFrame(1f, 1f);
        opacity.Duration = TimeSpan.FromMilliseconds(durationMs);

        var offset = compositor.CreateVector3KeyFrameAnimation();
        offset.InsertKeyFrame(0f, new Vector3((float)-offsetPixels, 0, 0));
        offset.InsertKeyFrame(1f, Vector3.Zero, easing);
        offset.Duration = TimeSpan.FromMilliseconds(durationMs);

        visual.StartAnimation("Opacity", opacity);
        visual.StartAnimation("Offset", offset);
    }

    public static void Pulse(UIElement element, int durationMs = SlowDurationMs)    {
        if (element is null || !IsEnabled)
        {
            return;
        }

        var visual = ElementCompositionPreview.GetElementVisual(element);
        var compositor = visual.Compositor;
        var easing = CompositionEasingFunction.CreateCubicBezierEasingFunction(
            compositor,
            new Vector2(0.3f, 0f),
            new Vector2(0.7f, 1f));

        var animation = compositor.CreateScalarKeyFrameAnimation();
        animation.InsertKeyFrame(0f, 1f);
        animation.InsertKeyFrame(0.5f, 0.45f);
        animation.InsertKeyFrame(1f, 1f, easing);
        animation.Duration = TimeSpan.FromMilliseconds(durationMs);
        animation.IterationBehavior = AnimationIterationBehavior.Forever;

        visual.StartAnimation("Opacity", animation);
    }
}
