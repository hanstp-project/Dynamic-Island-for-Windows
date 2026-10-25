using System;
using System.Windows;
using WindowsDynamicIsland.Models;
using WindowsDynamicIsland.Services;
using WindowsDynamicIsland.Themes;

namespace WindowsDynamicIsland;

public partial class SettingsWindow : Window
{
    private readonly SettingsService _settingsService;
    private readonly StartupService _startupService;

    public SettingsWindow(SettingsService settingsService, StartupService startupService)
    {
        InitializeComponent();
        _settingsService = settingsService;
        _startupService = startupService;
        LoadFromSettings(_settingsService.Current);
    }

    private void LoadFromSettings(AppSettings s)
    {
        StyleCombo.SelectedIndex = s.Style == IslandStyle.Notch ? 1 : 0;
        ThemeCombo.SelectedIndex = s.Theme == AppTheme.Light ? 1 : 0;
        CompactWidthSlider.Value = s.CompactWidth;
        CompactHeightSlider.Value = s.CompactHeight;
        ExpandedWidthSlider.Value = s.ExpandedWidth;
        TopOffsetSlider.Value = s.TopOffset;
        OpacitySlider.Value = s.Opacity;

        MediaEnabledCheck.IsChecked = s.MediaEnabled;
        ShowVisualizerCheck.IsChecked = s.ShowVisualizer;
        ShowCompactLyricsCheck.IsChecked = s.ShowCompactLyrics;
        SystemMonitorCheck.IsChecked = s.SystemMonitorEnabled;
        BatteryCheck.IsChecked = s.BatteryEnabled;
        ClockCheck.IsChecked = s.ClockEnabled;

        NotificationsEnabledCheck.IsChecked = s.NotificationsEnabled;
        MirrorWindowsNotificationsCheck.IsChecked = s.MirrorWindowsNotifications;
        NotificationTimeoutSlider.Value = s.NotificationTimeoutSeconds;

        ExpandOnHoverCheck.IsChecked = s.ExpandOnHover;
        AutoCollapseSlider.Value = s.AutoCollapseSeconds;
        StartWithWindowsCheck.IsChecked = _startupService.IsEnabled();
        ShowOnAllMonitorsCheck.IsChecked = s.ShowOnAllMonitors;
        HideInFullscreenCheck.IsChecked = s.HideInFullscreen;
        ReducedMotionCheck.IsChecked = s.ReducedMotion;
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        var s = _settingsService.Current;
        s.Style = StyleCombo.SelectedIndex == 1 ? IslandStyle.Notch : IslandStyle.DynamicIsland;
        s.Theme = ThemeCombo.SelectedIndex == 1 ? AppTheme.Light : AppTheme.Dark;
        s.CompactWidth = CompactWidthSlider.Value;
        s.CompactHeight = CompactHeightSlider.Value;
        s.ExpandedWidth = ExpandedWidthSlider.Value;
        s.TopOffset = TopOffsetSlider.Value;
        s.Opacity = OpacitySlider.Value;

        s.MediaEnabled = MediaEnabledCheck.IsChecked ?? true;
        s.ShowVisualizer = ShowVisualizerCheck.IsChecked ?? true;
        s.ShowCompactLyrics = ShowCompactLyricsCheck.IsChecked ?? true;
        s.SystemMonitorEnabled = SystemMonitorCheck.IsChecked ?? true;
        s.BatteryEnabled = BatteryCheck.IsChecked ?? true;
        s.ClockEnabled = ClockCheck.IsChecked ?? true;

        s.NotificationsEnabled = NotificationsEnabledCheck.IsChecked ?? true;
        s.MirrorWindowsNotifications = MirrorWindowsNotificationsCheck.IsChecked ?? true;
        s.NotificationTimeoutSeconds = (int)NotificationTimeoutSlider.Value;

        s.ExpandOnHover = ExpandOnHoverCheck.IsChecked ?? false;
        s.AutoCollapseSeconds = (int)AutoCollapseSlider.Value;
        s.ShowOnAllMonitors = ShowOnAllMonitorsCheck.IsChecked ?? true;
        s.HideInFullscreen = HideInFullscreenCheck.IsChecked ?? true;
        s.ReducedMotion = ReducedMotionCheck.IsChecked ?? false;

        bool startWithWin = StartWithWindowsCheck.IsChecked ?? false;
        s.StartWithWindows = startWithWin;
        _startupService.SetEnabled(startWithWin);

        _settingsService.NotifyChanged(null);
        ThemeManager.Apply(Application.Current.Resources, s.Theme);

        Close();
    }

    private void TestNotificationButton_Click(object sender, RoutedEventArgs e)
    {
        int seconds = (int)NotificationTimeoutSlider.Value;
        var testInfo = new NotificationInfo
        {
            Kind = NotificationKind.Windows,
            AppName = "WhatsApp",
            Title = "Sarah Jenkins",
            Message = "Hey! Sesuai kesepakatan meeting kita jam 10 pagi ya ☕",
            IconGlyph = "💬",
            AccentColor = "#25D366",
            Timeout = TimeSpan.FromSeconds(seconds),
            CreatedAt = DateTimeOffset.Now
        };

        if (Application.Current is App app)
        {
            app.BroadcastNotification(testInfo);
        }
    }

    private void ResetButton_Click(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show("Are you sure you want to restore default settings?", "Reset Settings",
            MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
        {
            var def = new AppSettings();
            LoadFromSettings(def);
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
