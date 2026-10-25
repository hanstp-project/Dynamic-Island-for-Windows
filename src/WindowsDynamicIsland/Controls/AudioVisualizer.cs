using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace WindowsDynamicIsland.Controls;

/// <summary>
/// The little "now playing" waveform shown in the Dynamic Island, tinted with the artwork colour.
/// Animations only run while <see cref="SetActive"/> is true (and are capped at 30 fps), so a paused
/// or hidden visualizer costs nothing.
/// </summary>
public sealed class AudioVisualizer : StackPanel
{
    private static readonly double[][] Patterns =
    {
        new[] { 0.35, 0.85, 0.50, 1.00, 0.40, 0.70, 0.35 },
        new[] { 0.60, 0.30, 1.00, 0.55, 0.90, 0.35, 0.60 },
        new[] { 0.45, 1.00, 0.35, 0.80, 0.50, 0.95, 0.45 },
        new[] { 0.80, 0.40, 0.70, 0.30, 1.00, 0.50, 0.80 },
        new[] { 0.30, 0.65, 0.95, 0.45, 0.60, 0.85, 0.30 },
    };
    private static readonly double[] Durations = { 1.15, 0.95, 1.30, 1.05, 1.20 };

    public static readonly DependencyProperty BarBrushProperty = DependencyProperty.Register(
        nameof(BarBrush), typeof(Brush), typeof(AudioVisualizer),
        new PropertyMetadata(Brushes.White, (d, e) => ((AudioVisualizer)d).ApplyBrush()));

    private readonly List<(Rectangle Rect, ScaleTransform Scale)> _bars = new();
    private bool? _active;

    public AudioVisualizer()
    {
        Orientation = Orientation.Horizontal;
        for (int i = 0; i < Patterns.Length; i++)
        {
            var scale = new ScaleTransform(1, 0.3);
            var rect = new Rectangle
            {
                Width = 3,
                RadiusX = 1.5,
                RadiusY = 1.5,
                Margin = new Thickness(1, 0, 1, 0),
                RenderTransformOrigin = new Point(0.5, 0.5),
                RenderTransform = scale,
                Fill = BarBrush,
            };
            Children.Add(rect);
            _bars.Add((rect, scale));
        }
        SetActive(false, false);
    }

    public Brush BarBrush { get => (Brush)GetValue(BarBrushProperty); set => SetValue(BarBrushProperty, value); }

    private void ApplyBrush()
    {
        foreach (var (rect, _) in _bars) rect.Fill = BarBrush;
    }

    public void SetActive(bool playing, bool reducedMotion)
    {
        bool animate = playing && !reducedMotion;
        if (_active == animate && !(playing && reducedMotion)) return;
        _active = animate;

        for (int i = 0; i < _bars.Count; i++)
        {
            var scale = _bars[i].Scale;
            if (!animate)
            {
                scale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
                scale.ScaleY = playing ? 0.6 : 0.3;
                continue;
            }

            var pattern = Patterns[i];
            var total = TimeSpan.FromSeconds(Durations[i]);
            var anim = new DoubleAnimationUsingKeyFrames { RepeatBehavior = RepeatBehavior.Forever, Duration = total };
            var ease = new SineEase { EasingMode = EasingMode.EaseInOut };
            ease.Freeze();
            for (int k = 0; k < pattern.Length; k++)
            {
                var t = TimeSpan.FromTicks(total.Ticks * k / (pattern.Length - 1));
                anim.KeyFrames.Add(new EasingDoubleKeyFrame(pattern[k], KeyTime.FromTimeSpan(t), ease));
            }
            Timeline.SetDesiredFrameRate(anim, 30);
            anim.Freeze();
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, anim);
        }
    }
}
