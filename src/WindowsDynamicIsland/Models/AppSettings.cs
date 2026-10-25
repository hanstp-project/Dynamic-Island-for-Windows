using System;
using System.Text.Json.Serialization;

namespace WindowsDynamicIsland.Models;

public enum AppTheme { Dark, Light }
public enum ClockFormat { System, TwelveHour, TwentyFourHour }
public enum SettingsCloseBehavior { HideToTray, ExitApplication }
public enum IslandStyle { DynamicIsland, Notch }

/// <summary>
/// All user-configurable settings. Persisted as JSON in
/// %LOCALAPPDATA%\WindowsDynamicIsland\settings.json.
/// Always call <see cref="Validate"/> after deserialising – values are clamped to safe ranges.
/// </summary>
public sealed class AppSettings
{
    public const int CurrentVersion = 1;

    // ----- Valid ranges (shared with the settings UI) -----
    public const double MinCompactWidth = 90, MaxCompactWidth = 220;
    public const double MinCompactHeight = 26, MaxCompactHeight = 44;
    public const double MinExpandedWidth = 320, MaxExpandedWidth = 520;
    public const double MinTopOffset = 0, MaxTopOffset = 80;
    public const double MinCornerRadius = 12, MaxCornerRadius = 40;
    public const double MinAnimationSpeed = 0.5, MaxAnimationSpeed = 2.0;
    public const double MinOpacity = 0.6, MaxOpacity = 1.0;
    public const int MinAutoCollapse = 1, MaxAutoCollapse = 60;
    public const int MinNotificationTimeout = 1, MaxNotificationTimeout = 30;
    public const int MinHoverDelay = 0, MaxHoverDelay = 1500;

    public int SettingsVersion { get; set; } = CurrentVersion;
    public bool FirstRunCompleted { get; set; }

    // ----- General -----
    /// <summary>Mirrors the HKCU Run entry. The registry is the source of truth at startup.</summary>
    public bool StartWithWindows { get; set; }
    /// <summary>When true, manual launches go straight to the tray (settings window not opened).</summary>
    public bool LaunchMinimized { get; set; } = true;
    public bool ShowPillOnStartup { get; set; } = true;
    public bool ExpandOnStartup { get; set; } = true;
    public SettingsCloseBehavior CloseBehavior { get; set; } = SettingsCloseBehavior.HideToTray;

    // ----- Appearance -----
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public AppTheme Theme { get; set; } = AppTheme.Dark;
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public IslandStyle Style { get; set; } = IslandStyle.Notch;
    public double CompactWidth { get; set; } = 136;
    public double CompactHeight { get; set; } = 32;
    public double ExpandedWidth { get; set; } = 380;
    public double TopOffset { get; set; } = 8;
    public double CornerRadius { get; set; } = 28;
    /// <summary>Animation speed multiplier: 1 = normal, 2 = twice as fast.</summary>
    public double AnimationSpeed { get; set; } = 1.0;
    public double Opacity { get; set; } = 1.0;
    public bool ShowIdleIndicator { get; set; } = true;
    public bool ShowIdleClock { get; set; } = true;
    public bool ShowVisualizer { get; set; } = true;
    /// <summary>0 = primary display, 1..N = display number (falls back to primary if missing).</summary>
    public int MonitorIndex { get; set; }
    /// <summary>When true, creates and displays Dynamic Island on every connected monitor simultaneously.</summary>
    public bool ShowOnAllMonitors { get; set; } = true;

    // ----- Features -----
    public bool MediaEnabled { get; set; } = true;
    public bool ShowTrackChangePopup { get; set; } = true;
    /// <summary>When true, displays synchronized song lyrics in the compact island pill instead of just the track title.</summary>
    public bool ShowCompactLyrics { get; set; } = true;
    public bool NotificationsEnabled { get; set; } = true;
    public int NotificationTimeoutSeconds { get; set; } = 3;
    /// <summary>Opt-in: try to mirror Windows toast notifications (requires OS permission).</summary>
    public bool MirrorWindowsNotifications { get; set; } = true;
    public bool SystemMonitorEnabled { get; set; } = true;
    public bool BatteryEnabled { get; set; } = true;
    public bool NetworkStatusEnabled { get; set; } = true;
    public bool ClockEnabled { get; set; } = true;
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ClockFormat ClockFormat { get; set; } = ClockFormat.System;
    public bool ShowDayOfWeek { get; set; } = true;
    public bool ClipboardShortcutEnabled { get; set; } = true;

    // ----- Behavior -----
    public bool ExpandOnHover { get; set; }
    public int HoverDelayMs { get; set; } = 350;
    public bool ExpandOnClick { get; set; } = true;
    public int AutoCollapseSeconds { get; set; } = 15;
    public bool AlwaysOnTop { get; set; } = true;
    public bool HideInFullscreen { get; set; } = false;
    public bool ReducedMotion { get; set; }

    /// <summary>Clamps every value to its valid range. Returns true if anything was corrected.</summary>
    public bool Validate()
    {
        bool changed = false;
        CompactWidth = Clamp(CompactWidth, MinCompactWidth, MaxCompactWidth, 136, ref changed);
        CompactHeight = Clamp(CompactHeight, MinCompactHeight, MaxCompactHeight, 32, ref changed);
        ExpandedWidth = Clamp(ExpandedWidth, MinExpandedWidth, MaxExpandedWidth, 380, ref changed);
        TopOffset = Clamp(TopOffset, MinTopOffset, MaxTopOffset, 8, ref changed);
        CornerRadius = Clamp(CornerRadius, MinCornerRadius, MaxCornerRadius, 28, ref changed);
        AnimationSpeed = Clamp(AnimationSpeed, MinAnimationSpeed, MaxAnimationSpeed, 1.0, ref changed);
        Opacity = Clamp(Opacity, MinOpacity, MaxOpacity, 1.0, ref changed);
        AutoCollapseSeconds = ClampInt(AutoCollapseSeconds, MinAutoCollapse, MaxAutoCollapse, ref changed);
        NotificationTimeoutSeconds = ClampInt(NotificationTimeoutSeconds, MinNotificationTimeout, MaxNotificationTimeout, ref changed);
        HoverDelayMs = ClampInt(HoverDelayMs, MinHoverDelay, MaxHoverDelay, ref changed);
        if (MonitorIndex < 0 || MonitorIndex > 16) { MonitorIndex = 0; changed = true; }
        if (!Enum.IsDefined(Theme)) { Theme = AppTheme.Dark; changed = true; }
        if (!Enum.IsDefined(Style)) { Style = IslandStyle.DynamicIsland; changed = true; }
        if (!Enum.IsDefined(ClockFormat)) { ClockFormat = ClockFormat.System; changed = true; }
        if (!Enum.IsDefined(CloseBehavior)) { CloseBehavior = SettingsCloseBehavior.HideToTray; changed = true; }
        if (!ExpandOnHover && !ExpandOnClick) { ExpandOnClick = true; changed = true; } // never make the island unreachable
        if (SettingsVersion != CurrentVersion) { SettingsVersion = CurrentVersion; changed = true; }
        return changed;
    }

    private static double Clamp(double value, double min, double max, double fallback, ref bool changed)
    {
        if (double.IsNaN(value) || double.IsInfinity(value)) { changed = true; return fallback; }
        if (value < min) { changed = true; return min; }
        if (value > max) { changed = true; return max; }
        return value;
    }

    private static int ClampInt(int value, int min, int max, ref bool changed)
    {
        if (value < min) { changed = true; return min; }
        if (value > max) { changed = true; return max; }
        return value;
    }

    public AppSettings Clone() => (AppSettings)MemberwiseClone();
}
