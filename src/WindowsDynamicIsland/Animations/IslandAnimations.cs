using System;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using WindowsDynamicIsland.Controls;

namespace WindowsDynamicIsland.Animations;

/// <summary>Timing configuration derived from settings + OS accessibility preferences.</summary>
public readonly record struct AnimationProfile(double Speed, bool ReducedMotion)
{
    public static AnimationProfile From(Models.AppSettings s)
        => new(s.AnimationSpeed, s.ReducedMotion || !SystemParameters.ClientAreaAnimation);

    public TimeSpan Scale(double ms) => TimeSpan.FromMilliseconds(ms / Math.Max(0.1, Speed));
}

/// <summary>
/// All WPF animation plumbing for the island.
/// Pattern used everywhere: read the *current* (possibly animated) value, set the base value to the
/// target, then run a FillBehavior.Stop animation from current → target with SnapshotAndReplace.
/// This makes interruption/reversal seamless (no jumps) and leaves no animation clocks alive once
/// finished, so the idle island consumes no rendering time.
/// </summary>
public static class IslandAnimations
{
    private const double GrowMs = 540, ShrinkMs = 420, ReducedMs = 140;
    private const double FadeInMs = 200, FadeInDelayMs = 80, FadeOutMs = 110;

    private static readonly ConditionalWeakTable<FrameworkElement, StrongBox<int>> LayerTokens = new();

    public static void AnimateGeometry(IslandPill pill, IslandGeometry target, bool growing, AnimationProfile profile)
    {
        IEasingFunction ease;
        TimeSpan duration;
        if (profile.ReducedMotion)
        {
            ease = Frozen(new CubicEase { EasingMode = EasingMode.EaseOut });
            duration = TimeSpan.FromMilliseconds(ReducedMs);
        }
        else
        {
            ease = Frozen(new SpringEase { DampingRatio = growing ? 0.72 : 0.94 });
            duration = profile.Scale(growing ? GrowMs : ShrinkMs);
        }

        AnimateTo(pill, IslandPill.PillWidthProperty, target.Width, duration, ease);
        AnimateTo(pill, IslandPill.PillHeightProperty, target.Height, duration, ease);
        AnimateTo(pill, IslandPill.PillRadiusProperty, target.CornerRadius, duration, ease);
        AnimateTo(pill, UIElement.OpacityProperty, target.Opacity,
            profile.ReducedMotion ? TimeSpan.FromMilliseconds(ReducedMs) : profile.Scale(220),
            Frozen(new CubicEase { EasingMode = EasingMode.EaseOut }));
    }

    /// <summary>Jump straight to a geometry (used on first show / settings changes while hidden).</summary>
    public static void SetGeometry(IslandPill pill, IslandGeometry g)
    {
        pill.BeginAnimation(IslandPill.PillWidthProperty, null);
        pill.BeginAnimation(IslandPill.PillHeightProperty, null);
        pill.BeginAnimation(IslandPill.PillRadiusProperty, null);
        pill.BeginAnimation(UIElement.OpacityProperty, null);
        pill.PillWidth = g.Width;
        pill.PillHeight = g.Height;
        pill.PillRadius = g.CornerRadius;
        pill.Opacity = g.Opacity;
    }

    /// <summary>Fades + gently scales a content layer in, after a short delay so the pill leads.</summary>
    public static void ShowLayer(FrameworkElement layer, AnimationProfile profile)
    {
        NextToken(layer);
        layer.Visibility = Visibility.Visible;
        layer.IsHitTestVisible = true;

        double from = layer.Opacity;
        if (profile.ReducedMotion)
        {
            AnimateTo(layer, UIElement.OpacityProperty, 1, TimeSpan.FromMilliseconds(100), null);
            return;
        }

        var delay = profile.Scale(FadeInDelayMs);
        var dur = profile.Scale(FadeInMs);
        var ease = Frozen(new CubicEase { EasingMode = EasingMode.EaseOut });

        layer.Opacity = 1;
        var fade = new DoubleAnimationUsingKeyFrames { FillBehavior = FillBehavior.Stop };
        fade.KeyFrames.Add(new DiscreteDoubleKeyFrame(from, KeyTime.FromTimeSpan(TimeSpan.Zero)));
        fade.KeyFrames.Add(new LinearDoubleKeyFrame(from, KeyTime.FromTimeSpan(delay)));
        fade.KeyFrames.Add(new EasingDoubleKeyFrame(1, KeyTime.FromTimeSpan(delay + dur), ease));
        fade.Freeze();
        layer.BeginAnimation(UIElement.OpacityProperty, fade, HandoffBehavior.SnapshotAndReplace);

        if (layer.RenderTransform is ScaleTransform scale && !scale.IsFrozen)
        {
            var sEase = Frozen(new SpringEase { DampingRatio = 0.8 });
            double sFrom = from < 0.05 ? 0.94 : scale.ScaleX;
            scale.ScaleX = scale.ScaleY = sFrom;
            AnimateTo(scale, ScaleTransform.ScaleXProperty, 1, delay + dur + profile.Scale(120), sEase);
            AnimateTo(scale, ScaleTransform.ScaleYProperty, 1, delay + dur + profile.Scale(120), sEase);
        }
    }

    /// <summary>Fades a layer out and collapses it afterwards (unless it was shown again meanwhile).</summary>
    public static void HideLayer(FrameworkElement layer, AnimationProfile profile)
    {
        if (layer.Visibility != Visibility.Visible && layer.Opacity <= 0.001) return;
        int token = NextToken(layer);
        layer.IsHitTestVisible = false;

        double from = layer.Opacity;
        layer.Opacity = 0;
        var anim = new DoubleAnimation(from, 0, profile.ReducedMotion ? TimeSpan.FromMilliseconds(80) : profile.Scale(FadeOutMs))
        {
            FillBehavior = FillBehavior.Stop,
            EasingFunction = Frozen(new CubicEase { EasingMode = EasingMode.EaseIn }),
        };
        anim.Completed += (_, _) =>
        {
            if (CurrentToken(layer) == token) layer.Visibility = Visibility.Collapsed;
        };
        layer.BeginAnimation(UIElement.OpacityProperty, anim, HandoffBehavior.SnapshotAndReplace);
    }

    /// <summary>Immediately shows/hides a layer without animation.</summary>
    public static void SetLayerVisible(FrameworkElement layer, bool visible)
    {
        NextToken(layer);
        layer.BeginAnimation(UIElement.OpacityProperty, null);
        layer.Opacity = visible ? 1 : 0;
        layer.IsHitTestVisible = visible;
        layer.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        if (layer.RenderTransform is ScaleTransform s && !s.IsFrozen)
        {
            s.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            s.BeginAnimation(ScaleTransform.ScaleYProperty, null);
            s.ScaleX = s.ScaleY = 1;
        }
    }

    public static void AnimateTo(IAnimatable target, DependencyProperty dp, double to, TimeSpan duration, IEasingFunction? ease)
    {
        var obj = (DependencyObject)target;
        double from = (double)obj.GetValue(dp);
        obj.SetValue(dp, to);
        if (duration <= TimeSpan.Zero || Math.Abs(from - to) < 0.01)
        {
            target.BeginAnimation(dp, null);
            return;
        }
        var anim = new DoubleAnimation(from, to, duration) { EasingFunction = ease, FillBehavior = FillBehavior.Stop };
        anim.Freeze();
        target.BeginAnimation(dp, anim, HandoffBehavior.SnapshotAndReplace);
    }

    private static T Frozen<T>(T freezable) where T : Freezable
    {
        freezable.Freeze();
        return freezable;
    }

    private static int NextToken(FrameworkElement el) => ++LayerTokens.GetOrCreateValue(el).Value;
    private static int CurrentToken(FrameworkElement el) => LayerTokens.GetOrCreateValue(el).Value;
}
