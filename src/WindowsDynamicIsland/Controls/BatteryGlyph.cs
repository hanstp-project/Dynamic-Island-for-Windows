using System;
using System.Windows;
using System.Windows.Media;

namespace WindowsDynamicIsland.Controls;

/// <summary>iOS-style battery icon: rounded outline, proportional fill and a terminal nub.</summary>
public sealed class BatteryGlyph : FrameworkElement
{
    public static readonly DependencyProperty PercentProperty = DependencyProperty.Register(
        nameof(Percent), typeof(double), typeof(BatteryGlyph),
        new FrameworkPropertyMetadata(100.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty FillBrushProperty = DependencyProperty.Register(
        nameof(FillBrush), typeof(Brush), typeof(BatteryGlyph),
        new FrameworkPropertyMetadata(Brushes.White, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty OutlineBrushProperty = DependencyProperty.Register(
        nameof(OutlineBrush), typeof(Brush), typeof(BatteryGlyph),
        new FrameworkPropertyMetadata(Brushes.Gray, FrameworkPropertyMetadataOptions.AffectsRender));

    public BatteryGlyph()
    {
        Width = 25;
        Height = 12;
    }

    public double Percent { get => (double)GetValue(PercentProperty); set => SetValue(PercentProperty, value); }
    public Brush FillBrush { get => (Brush)GetValue(FillBrushProperty); set => SetValue(FillBrushProperty, value); }
    public Brush OutlineBrush { get => (Brush)GetValue(OutlineBrushProperty); set => SetValue(OutlineBrushProperty, value); }

    protected override void OnRender(DrawingContext dc)
    {
        double w = ActualWidth, h = ActualHeight;
        if (w < 8 || h < 5) return;
        double nub = Math.Max(1.5, w * 0.07);
        double bodyW = w - nub - 1;
        double r = h * 0.3;

        var body = new Rect(0.5, 0.5, bodyW - 1, h - 1);
        dc.DrawRoundedRectangle(null, new Pen(OutlineBrush, 1), body, r, r);
        dc.DrawRoundedRectangle(OutlineBrush, null, new Rect(bodyW + 0.5, h * 0.33, nub, h * 0.34), nub / 2, nub / 2);

        const double inset = 2;
        double frac = Math.Clamp(Percent / 100.0, 0, 1);
        double innerW = (body.Width - inset * 2) * frac;
        if (innerW > 0.5)
        {
            double ir = Math.Max(0.5, r - 1.5);
            dc.DrawRoundedRectangle(FillBrush, null,
                new Rect(body.X + inset, body.Y + inset, innerW, body.Height - inset * 2), ir, ir);
        }
    }
}
