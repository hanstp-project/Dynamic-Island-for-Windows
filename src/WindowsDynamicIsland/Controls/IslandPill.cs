using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using WindowsDynamicIsland.Models;

namespace WindowsDynamicIsland.Controls;

/// <summary>
/// The pill / notch surface. Exposes animatable doubles (PillWidth/Height/Radius) because
/// <see cref="CornerRadius"/> itself is not animatable. Adapts shape seamlessly between
/// floating Dynamic Island and attached Notch styles with authentic MacBook-style concave top fillets.
/// </summary>
public class IslandPill : ContentControl
{
    public static readonly DependencyProperty PillWidthProperty = DependencyProperty.Register(
        nameof(PillWidth), typeof(double), typeof(IslandPill), new PropertyMetadata(124.0, OnGeometryChanged));

    public static readonly DependencyProperty PillHeightProperty = DependencyProperty.Register(
        nameof(PillHeight), typeof(double), typeof(IslandPill), new PropertyMetadata(32.0, OnGeometryChanged));

    public static readonly DependencyProperty PillRadiusProperty = DependencyProperty.Register(
        nameof(PillRadius), typeof(double), typeof(IslandPill), new PropertyMetadata(16.0, OnGeometryChanged));

    public static readonly DependencyProperty SurfaceOpacityProperty = DependencyProperty.Register(
        nameof(SurfaceOpacity), typeof(double), typeof(IslandPill), new PropertyMetadata(1.0, OnSurfaceOpacityChanged));

    public static readonly DependencyProperty StyleModeProperty = DependencyProperty.Register(
        nameof(StyleMode), typeof(IslandStyle), typeof(IslandPill), new PropertyMetadata(IslandStyle.DynamicIsland, OnGeometryChanged));

    private const double EarRadius = 10.0;
    private const double EarOverlap = 2.0;
    private const double EarWidth = EarRadius + EarOverlap; // 12.0
    private const double EarHeight = EarRadius;             // 10.0

    private static readonly PathGeometry EarLeftGeometry = CreateEarLeftGeometry();
    private static readonly PathGeometry EarRightGeometry = CreateEarRightGeometry();

    private Border? _shadowBorder;
    private Border? _surface;
    private System.Windows.Shapes.Path? _earLeft;
    private System.Windows.Shapes.Path? _earRight;
    private ContentPresenter? _host;
    private readonly RectangleGeometry _clip = new();

    public IslandPill()
    {
        HorizontalAlignment = HorizontalAlignment.Center;
        VerticalAlignment = VerticalAlignment.Top;
        SnapsToDevicePixels = false;
        UseLayoutRounding = false;

        var gridFactory = new FrameworkElementFactory(typeof(Grid));

        // 1. Drop Shadow
        var shadowFactory = new FrameworkElementFactory(typeof(Border), "PART_ShadowBorder");
        shadowFactory.SetResourceReference(Border.BackgroundProperty, "Island.Background.Solid");
        shadowFactory.SetResourceReference(UIElement.OpacityProperty, "Island.Shadow.Opacity");
        var shadow = new DropShadowEffect
        {
            BlurRadius = 20,
            ShadowDepth = 5,
            Direction = 270,
            Color = Colors.Black,
            Opacity = 1,
            RenderingBias = RenderingBias.Performance
        };
        shadow.Freeze();
        shadowFactory.SetValue(Border.EffectProperty, shadow);

        // 2. Main Pill / Notch Surface
        var surfaceFactory = new FrameworkElementFactory(typeof(Border), "PART_Surface");
        surfaceFactory.SetResourceReference(Border.BackgroundProperty, "Island.Background");
        surfaceFactory.SetResourceReference(Border.BorderBrushProperty, "Island.Edge");
        surfaceFactory.SetValue(Border.BorderThicknessProperty, new Thickness(1));

        // 3. Notch Top-Left Ear Fillet (Concave curve meeting top bezel)
        var earLeftFactory = new FrameworkElementFactory(typeof(System.Windows.Shapes.Path), "PART_EarLeft");
        earLeftFactory.SetResourceReference(System.Windows.Shapes.Path.FillProperty, "Island.Background");
        earLeftFactory.SetResourceReference(System.Windows.Shapes.Path.StrokeProperty, "Island.Edge");
        earLeftFactory.SetValue(System.Windows.Shapes.Path.StrokeThicknessProperty, 1.0);
        earLeftFactory.SetValue(System.Windows.Shapes.Path.StrokeStartLineCapProperty, PenLineCap.Flat);
        earLeftFactory.SetValue(System.Windows.Shapes.Path.StrokeEndLineCapProperty, PenLineCap.Flat);
        earLeftFactory.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Left);
        earLeftFactory.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Top);
        earLeftFactory.SetValue(FrameworkElement.WidthProperty, EarWidth);
        earLeftFactory.SetValue(FrameworkElement.HeightProperty, EarHeight);
        earLeftFactory.SetValue(FrameworkElement.MarginProperty, new Thickness(-EarRadius, 0, 0, 0));
        earLeftFactory.SetValue(UIElement.VisibilityProperty, Visibility.Collapsed);

        // 4. Notch Top-Right Ear Fillet (Concave curve meeting top bezel)
        var earRightFactory = new FrameworkElementFactory(typeof(System.Windows.Shapes.Path), "PART_EarRight");
        earRightFactory.SetResourceReference(System.Windows.Shapes.Path.FillProperty, "Island.Background");
        earRightFactory.SetResourceReference(System.Windows.Shapes.Path.StrokeProperty, "Island.Edge");
        earRightFactory.SetValue(System.Windows.Shapes.Path.StrokeThicknessProperty, 1.0);
        earRightFactory.SetValue(System.Windows.Shapes.Path.StrokeStartLineCapProperty, PenLineCap.Flat);
        earRightFactory.SetValue(System.Windows.Shapes.Path.StrokeEndLineCapProperty, PenLineCap.Flat);
        earRightFactory.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Right);
        earRightFactory.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Top);
        earRightFactory.SetValue(FrameworkElement.WidthProperty, EarWidth);
        earRightFactory.SetValue(FrameworkElement.HeightProperty, EarHeight);
        earRightFactory.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 0, -EarRadius, 0));
        earRightFactory.SetValue(UIElement.VisibilityProperty, Visibility.Collapsed);

        // 5. Host Content Presenter
        var hostFactory = new FrameworkElementFactory(typeof(ContentPresenter), "PART_Host");
        hostFactory.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Stretch);
        hostFactory.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Stretch);

        gridFactory.AppendChild(shadowFactory);
        gridFactory.AppendChild(surfaceFactory);
        gridFactory.AppendChild(earLeftFactory);
        gridFactory.AppendChild(earRightFactory);
        gridFactory.AppendChild(hostFactory);

        Template = new ControlTemplate(typeof(IslandPill)) { VisualTree = gridFactory };
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        _shadowBorder = GetTemplateChild("PART_ShadowBorder") as Border;
        _surface = GetTemplateChild("PART_Surface") as Border;
        _earLeft = GetTemplateChild("PART_EarLeft") as System.Windows.Shapes.Path;
        _earRight = GetTemplateChild("PART_EarRight") as System.Windows.Shapes.Path;
        _host = GetTemplateChild("PART_Host") as ContentPresenter;

        if (_earLeft != null)
        {
            _earLeft.Data = EarLeftGeometry;
            _earLeft.Opacity = SurfaceOpacity;
        }

        if (_earRight != null)
        {
            _earRight.Data = EarRightGeometry;
            _earRight.Opacity = SurfaceOpacity;
        }

        if (_surface != null)
        {
            _surface.Opacity = SurfaceOpacity;
        }

        ApplyGeometry();
    }

    public double PillWidth { get => (double)GetValue(PillWidthProperty); set => SetValue(PillWidthProperty, value); }
    public double PillHeight { get => (double)GetValue(PillHeightProperty); set => SetValue(PillHeightProperty, value); }
    public double PillRadius { get => (double)GetValue(PillRadiusProperty); set => SetValue(PillRadiusProperty, value); }
    public double SurfaceOpacity { get => (double)GetValue(SurfaceOpacityProperty); set => SetValue(SurfaceOpacityProperty, value); }
    public IslandStyle StyleMode { get => (IslandStyle)GetValue(StyleModeProperty); set => SetValue(StyleModeProperty, value); }

    private static void OnGeometryChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((IslandPill)d).ApplyGeometry();

    private static void OnSurfaceOpacityChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var pill = (IslandPill)d;
        double val = (double)e.NewValue;
        if (pill._surface != null) pill._surface.Opacity = val;
        if (pill._earLeft != null) pill._earLeft.Opacity = val;
        if (pill._earRight != null) pill._earRight.Opacity = val;
    }

    private void ApplyGeometry()
    {
        double w = Math.Max(1, PillWidth);
        double h = Math.Max(1, PillHeight);
        double r = Math.Clamp(PillRadius, 0, Math.Min(w, h) / 2);

        Width = w;
        Height = h;

        if (StyleMode == IslandStyle.Notch)
        {
            // Notch Island:
            // 1. Shows top-left and top-right concave ear fillets connecting smoothly to the top bezel
            // 2. Flat top edge flush against the screen bezel (BorderThickness top=0)
            // 3. Rounded bottom corners
            double notchRadius = (r > 16.0) ? 18.0 : Math.Min(r, 12.0);
            var notchCornerRadius = new CornerRadius(0, 0, notchRadius, notchRadius);

            if (_surface != null)
            {
                _surface.CornerRadius = notchCornerRadius;
                _surface.BorderThickness = new Thickness(1, 0, 1, 1);
                _surface.Visibility = Visibility.Visible;
            }

            if (_shadowBorder != null)
            {
                _shadowBorder.CornerRadius = notchCornerRadius;
                _shadowBorder.Visibility = Visibility.Visible;
            }

            if (_earLeft != null) _earLeft.Visibility = Visibility.Visible;
            if (_earRight != null) _earRight.Visibility = Visibility.Visible;

            // Content clipping: rectangular at top, rounded corners at bottom
            var hostGeom = new PathGeometry();
            var hostFig = new PathFigure { StartPoint = new Point(0, 0), IsClosed = true, IsFilled = true };
            hostFig.Segments.Add(new LineSegment(new Point(w, 0), false));
            hostFig.Segments.Add(new LineSegment(new Point(w, Math.Max(0, h - notchRadius)), false));
            hostFig.Segments.Add(new ArcSegment(new Point(Math.Max(0, w - notchRadius), h), new Size(notchRadius, notchRadius), 0, false, SweepDirection.Clockwise, false));
            hostFig.Segments.Add(new LineSegment(new Point(Math.Min(w, notchRadius), h), false));
            hostFig.Segments.Add(new ArcSegment(new Point(0, Math.Max(0, h - notchRadius)), new Size(notchRadius, notchRadius), 0, false, SweepDirection.Clockwise, false));
            hostGeom.Figures.Add(hostFig);
            hostGeom.Freeze();

            if (_host != null)
            {
                _host.Clip = hostGeom;
            }
        }
        else
        {
            // Dynamic Island mode: smooth floating capsule
            if (_earLeft != null) _earLeft.Visibility = Visibility.Collapsed;
            if (_earRight != null) _earRight.Visibility = Visibility.Collapsed;

            var cr = new CornerRadius(r);
            if (_surface != null)
            {
                _surface.CornerRadius = cr;
                _surface.BorderThickness = new Thickness(1);
                _surface.Visibility = Visibility.Visible;
            }

            if (_shadowBorder != null)
            {
                _shadowBorder.CornerRadius = cr;
                _shadowBorder.Visibility = Visibility.Visible;
            }

            _clip.Rect = new Rect(0, 0, w, h);
            _clip.RadiusX = _clip.RadiusY = r;

            if (_host != null)
            {
                _host.Clip = _clip;
            }
        }
    }

    /// <summary>
    /// Creates the geometry for the top-left ear fillet.
    /// Local coordinates [0, EarWidth] x [0, EarHeight].
    /// Overlap of EarOverlap (2px) lies on the right, inside the notch body.
    /// The arc curves smoothly from (EarRadius, EarHeight) [left edge of notch] to (0, 0) [top bezel].
    /// </summary>
    private static PathGeometry CreateEarLeftGeometry()
    {
        var geom = new PathGeometry();
        var fig = new PathFigure
        {
            StartPoint = new Point(0, 0),
            IsClosed = true,
            IsFilled = true
        };
        // Top edge flush with bezel, extending 2px inside notch
        fig.Segments.Add(new LineSegment(new Point(EarWidth, 0), false));
        // Vertical line inside notch
        fig.Segments.Add(new LineSegment(new Point(EarWidth, EarHeight), false));
        // Horizontal line meeting left edge of notch
        fig.Segments.Add(new LineSegment(new Point(EarRadius, EarHeight), false));
        // Smooth concave arc flaring out to top bezel (stroked with Island.Edge)
        fig.Segments.Add(new ArcSegment(
            new Point(0, 0),
            new Size(EarRadius, EarRadius),
            0,
            false,
            SweepDirection.Counterclockwise,
            true));
        geom.Figures.Add(fig);
        geom.Freeze();
        return geom;
    }

    /// <summary>
    /// Creates the geometry for the top-right ear fillet.
    /// Local coordinates [0, EarWidth] x [0, EarHeight].
    /// Overlap of EarOverlap (2px) lies on the left, inside the notch body.
    /// The arc curves smoothly from (EarOverlap, EarHeight) [right edge of notch] to (EarWidth, 0) [top bezel].
    /// </summary>
    private static PathGeometry CreateEarRightGeometry()
    {
        var geom = new PathGeometry();
        var fig = new PathFigure
        {
            StartPoint = new Point(0, 0),
            IsClosed = true,
            IsFilled = true
        };
        // Vertical line inside notch
        fig.Segments.Add(new LineSegment(new Point(0, EarHeight), false));
        // Horizontal line meeting right edge of notch
        fig.Segments.Add(new LineSegment(new Point(EarOverlap, EarHeight), false));
        // Smooth concave arc flaring out to top bezel (stroked with Island.Edge)
        fig.Segments.Add(new ArcSegment(
            new Point(EarWidth, 0),
            new Size(EarRadius, EarRadius),
            0,
            false,
            SweepDirection.Clockwise,
            true));
        // Top edge flush with bezel, connecting back to (0, 0)
        fig.Segments.Add(new LineSegment(new Point(0, 0), false));
        geom.Figures.Add(fig);
        geom.Freeze();
        return geom;
    }

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);
        ApplyGeometry();
    }

    protected override Size ArrangeOverride(Size arrangeBounds)
    {
        var size = base.ArrangeOverride(arrangeBounds);
        ApplyGeometry();
        return size;
    }
}
