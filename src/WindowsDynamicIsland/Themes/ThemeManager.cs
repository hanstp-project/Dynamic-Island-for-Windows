using System.Windows;
using System.Windows.Media;
using WindowsDynamicIsland.Models;

namespace WindowsDynamicIsland.Themes;

/// <summary>
/// Applies the island palette to application resources (consumed via DynamicResource).
/// Dark = Apple Dynamic Island: pure black surface, no visible edge, white primary text,
/// iOS "secondary label" grey, and Apple system accent colours (dark-mode variants).
/// Light is an optional inverted variant using the light-mode system colours.
/// </summary>
public static class ThemeManager
{
    public static void Apply(ResourceDictionary r, AppTheme theme)
    {
        bool d = theme == AppTheme.Dark;

        SetBrush(r, "Island.Background", d ? "#FF000000" : "#FFFFFFFF");
        SetBrush(r, "Island.Background.Solid", d ? "#FF000000" : "#FFFFFFFF");
        // Subtle glassmorphic rim so the island is crisp and elegant against dark and light wallpapers.
        SetBrush(r, "Island.Edge", d ? "#2EFFFFFF" : "#22000000");
        r["Island.Shadow.Color"] = Colors.Black;
        r["Island.Shadow.Opacity"] = d ? 0.45 : 0.20;

        // iOS label colours (label / secondaryLabel / tertiaryLabel).
        SetBrush(r, "Text.Primary", d ? "#FFFFFFFF" : "#FF000000");
        SetBrush(r, "Text.Secondary", d ? "#99EBEBF5" : "#993C3C43");
        SetBrush(r, "Text.Tertiary", d ? "#4DEBEBF5" : "#4D3C3C43");

        SetBrush(r, "Fill.Track", d ? "#33FFFFFF" : "#1F000000");
        SetBrush(r, "Fill.Hover", d ? "#1FFFFFFF" : "#14000000");
        SetBrush(r, "Fill.Pressed", d ? "#33FFFFFF" : "#24000000");
        SetBrush(r, "Fill.Placeholder", d ? "#FF2C2C2E" : "#FFE5E5EA");

        // Apple system colours.
        SetBrush(r, "Accent.Green", d ? "#FF30D158" : "#FF34C759");
        SetBrush(r, "Accent.Blue", d ? "#FF0A84FF" : "#FF007AFF");
        SetBrush(r, "Accent.Orange", d ? "#FFFF9F0A" : "#FFFF9500");
        SetBrush(r, "Accent.Red", d ? "#FFFF453A" : "#FFFF3B30");
        SetBrush(r, "Accent.Yellow", d ? "#FFFFD60A" : "#FFFFCC00");
        SetBrush(r, "Accent.Pink", d ? "#FFFF375F" : "#FFFF2D55");

        // Retro Camera theme styling (adapts to Dark & Light modes)
        SetBrush(r, "Camera.Body", d ? "#FF1C1C1E" : "#FFF2F2F7");
        SetBrush(r, "Camera.BodyEdge", d ? "#2EFFFFFF" : "#1F000000");
    }

    public static Color Hex(string hex) => (Color)ColorConverter.ConvertFromString(hex);

    /// <summary>Looks up a theme brush from the running application (falls back to white).</summary>
    public static Brush Get(string key)
        => Application.Current?.TryFindResource(key) as Brush ?? Brushes.White;

    public static SolidColorBrush Frozen(Color c)
    {
        var b = new SolidColorBrush(c);
        b.Freeze();
        return b;
    }

    private static void SetBrush(ResourceDictionary r, string key, string hex) => r[key] = Frozen(Hex(hex));
}
