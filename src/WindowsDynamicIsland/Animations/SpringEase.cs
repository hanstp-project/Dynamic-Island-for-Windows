using System;
using System.Windows;
using System.Windows.Media.Animation;

namespace WindowsDynamicIsland.Animations;

/// <summary>
/// Critically-/under-damped spring curve normalised to settle at t = 1.
/// DampingRatio 1.0 = no overshoot, 0.7 = gentle overshoot (playful expansion).
/// Implemented in EaseInCore with EasingMode = EaseIn so WPF uses the curve as-is.
/// </summary>
public sealed class SpringEase : EasingFunctionBase
{
    public static readonly DependencyProperty DampingRatioProperty =
        DependencyProperty.Register(nameof(DampingRatio), typeof(double), typeof(SpringEase), new PropertyMetadata(0.75));

    public SpringEase() => EasingMode = EasingMode.EaseIn;

    public double DampingRatio
    {
        get => (double)GetValue(DampingRatioProperty);
        set => SetValue(DampingRatioProperty, value);
    }

    /// <summary>Pure function for unit tests.</summary>
    public static double Evaluate(double t, double zeta)
    {
        if (t <= 0) return 0;
        if (t >= 1) return 1;
        zeta = Math.Clamp(zeta, 0.2, 1.0);
        // Pick omega so the envelope e^(-zeta*omega*t) has decayed to ~0.1% at t = 1.
        double omega = 6.9 / zeta;
        if (zeta >= 0.999)
        {
            // Critically damped
            return 1 - Math.Exp(-omega * t) * (1 + omega * t);
        }
        double wd = omega * Math.Sqrt(1 - zeta * zeta);
        double envelope = Math.Exp(-zeta * omega * t);
        return 1 - envelope * (Math.Cos(wd * t) + zeta * omega / wd * Math.Sin(wd * t));
    }

    protected override double EaseInCore(double normalizedTime) => Evaluate(normalizedTime, DampingRatio);

    protected override Freezable CreateInstanceCore() => new SpringEase();
}
