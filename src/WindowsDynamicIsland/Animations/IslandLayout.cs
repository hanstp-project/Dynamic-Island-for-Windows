using System;
using WindowsDynamicIsland.Models;

namespace WindowsDynamicIsland.Animations;

/// <summary>Target geometry of the pill for a given state.</summary>
public readonly record struct IslandGeometry(double Width, double Height, double CornerRadius, double Opacity);

/// <summary>
/// Pure layout rules: maps (state, page, settings) → pill size. Unit-tested.
/// Content heights are fixed per page so content never reflows (no text clipping or
/// layout jumps) while the pill animates – the pill clips/reveals the content instead.
/// </summary>
public static class IslandLayout
{
    public const double MusicExtraWidth = 96;
    public const double HoverGrowWidth = 12;
    public const double HoverGrowHeight = 4;
    public const double SettingsExtraWidth = 64;

    public const double DashboardPageWidth = 620;
    public const double DashboardPageHeight = 224;

    public const double MediaPageWidth = 384;
    public const double MediaPageHeight = 250;

    public const double SystemPageWidth = 384;
    public const double SystemPageHeight = 184;

    public const double ClockPageWidth = 360;
    public const double ClockPageHeight = 172;

    public const double ToolsPageWidth = 410;
    public const double ToolsPageHeight = 200;

    public const double WeatherPageWidth = 410;
    public const double WeatherPageHeight = 200;

    public const double CameraPageWidth = 400;
    public const double CameraPageHeight = 340;

    public const double EmptyPageHeight = 128;
    public const double NotificationHeight = 64;
    public const double NotificationDetailsHeight = 140;

    /// <summary>Space around the pill reserved for the drop shadow.</summary>
    public const double ShadowMargin = 28;

    /// <summary>Largest height the pill can ever take (used to size the host window).</summary>
    public static double MaxPillHeight => Math.Max(DashboardPageHeight, Math.Max(MediaPageHeight, Math.Max(CameraPageHeight, Math.Max(ToolsPageHeight, WeatherPageHeight))));

    public static double NotificationWidth(AppSettings s) => Math.Max(s.CompactWidth + 160, s.ExpandedWidth);

    public static double PageWidth(ExpandedPage page, AppSettings s) => page switch
    {
        ExpandedPage.Dashboard => DashboardPageWidth,
        ExpandedPage.Media => MediaPageWidth,
        ExpandedPage.System => SystemPageWidth,
        ExpandedPage.Clock => ClockPageWidth,
        ExpandedPage.Tools => ToolsPageWidth,
        ExpandedPage.Weather => WeatherPageWidth,
        ExpandedPage.Camera => CameraPageWidth,
        _ => s.ExpandedWidth,
    };

    public static double PageHeight(ExpandedPage page) => page switch
    {
        ExpandedPage.Dashboard => DashboardPageHeight,
        ExpandedPage.Media => MediaPageHeight,
        ExpandedPage.System => SystemPageHeight,
        ExpandedPage.Clock => ClockPageHeight,
        ExpandedPage.Tools => ToolsPageHeight,
        ExpandedPage.Weather => WeatherPageHeight,
        ExpandedPage.Camera => CameraPageHeight,
        _ => EmptyPageHeight,
    };

    public static IslandGeometry For(IslandState state, ExpandedPage page, bool musicPlaying,
                                     bool notificationDetails, AppSettings s)
    {
        double cw = s.CompactWidth, ch = s.CompactHeight;
        double capsule = ch / 2;
        // Element opacity only encodes visibility. The user's transparency setting is applied
        // to the background surface (IslandPill.SurfaceOpacity) so text stays fully legible.
        const double opacity = 1.0;

        switch (state)
        {
            case IslandState.Idle:
                return new(cw, ch, capsule, opacity);

            case IslandState.MusicPlaying:
            {
                double extra = s.ShowCompactLyrics ? MusicExtraWidth + 36 : MusicExtraWidth;
                return new(cw + extra, ch, capsule, opacity);
            }

            case IslandState.Hovered:
            {
                double musicExtra = s.ShowCompactLyrics ? MusicExtraWidth + 36 : MusicExtraWidth;
                double baseW = musicPlaying ? cw + musicExtra : cw;
                double h = ch + HoverGrowHeight;
                return new(baseW + HoverGrowWidth, h, h / 2, opacity);
            }

            case IslandState.Settings:
                return new(cw + SettingsExtraWidth, ch, capsule, opacity);

            case IslandState.Notification:
            {
                double h = notificationDetails ? NotificationDetailsHeight : NotificationHeight;
                double w = notificationDetails ? s.ExpandedWidth : NotificationWidth(s);
                return new(w, h, Math.Min(s.CornerRadius, h / 2), opacity);
            }

            case IslandState.Expanded:
            case IslandState.SystemInformation:
            {
                var p = state == IslandState.SystemInformation ? ExpandedPage.System : page;
                double h = PageHeight(p);
                double w = PageWidth(p, s);
                double r = s.Style == IslandStyle.Notch ? 18 : Math.Min(s.CornerRadius, h / 2);
                return new(w, h, r, opacity);
            }

            case IslandState.TemporarilyHidden:
                return new(cw * 0.6, ch * 0.6, ch * 0.3, 0);

            default:
                return new(cw, ch, capsule, opacity);
        }
    }

    /// <summary>Host window size (DIPs) large enough for every state plus shadow.</summary>
    public static (double Width, double Height) HostSize(AppSettings s)
    {
        double maxExpW = Math.Max(DashboardPageWidth, s.ExpandedWidth);
        double w = Math.Max(maxExpW, NotificationWidth(s)) + ShadowMargin * 2;
        double top = s.Style == IslandStyle.Notch ? 0 : s.TopOffset;
        double h = top + MaxPillHeight + ShadowMargin + 280;
        return (Math.Ceiling(w), Math.Ceiling(h));
    }

    /// <summary>True when moving from a smaller to a larger footprint (used to pick spring vs. damped easing).</summary>
    public static bool IsGrowing(IslandGeometry from, IslandGeometry to)
        => to.Width * to.Height > from.Width * from.Height;
}
