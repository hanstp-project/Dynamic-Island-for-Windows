using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Threading.Tasks;
using WindowsDynamicIsland.Animations;
using WindowsDynamicIsland.Controls;
using WindowsDynamicIsland.Infrastructure;
using WindowsDynamicIsland.Interop;
using WindowsDynamicIsland.Models;
using WindowsDynamicIsland.Services;
using WindowsDynamicIsland.Themes;

namespace WindowsDynamicIsland;

public partial class MainWindow : Window
{
    private readonly SettingsService _settingsService;
    private readonly IslandStateMachine _stateMachine;
    private readonly MediaSessionService _mediaService;
    private readonly SystemMonitorService _systemService;
    private readonly StartupService _startupService;
    private readonly TimerService _timerService = new();
    private readonly WeatherService _weatherService = new();
    private readonly ReminderService _reminderService;

    private readonly DispatcherTimer _clockTimer;
    private readonly DispatcherTimer _timelineTimer;
    private readonly DispatcherTimer _hoverTimer;
    private readonly DispatcherTimer _autoCollapseTimer;
    private readonly DispatcherTimer _notificationTimer;

    private IslandGeometry _currentGeometry;
    private SettingsWindow? _settingsWindow;
    private bool _isClosing;
    private bool _isUpdatingHardwareSliders;
    private readonly LyricsService _lyricsService = new();
    private bool _isLyricsFullMode;
    private string _lastTrackKey = "";
    private int _lastLyricIndex = -999;
    private int _lastCompactLyricIndex = -999;
    private string _lastLyricLine = "";
    private string _lastCompactLyricLine = "";
    private TimeSpan _lastEstimatedPosition = TimeSpan.Zero;

    // Timeline Scrubber & Click-to-Seek Lyrics State
    private bool _isDraggingScrubber;
    private TimeSpan? _dragTargetPosition;
    private DateTimeOffset _seekLockUntil = DateTimeOffset.MinValue;
    private TimeSpan _soughtTargetPosition = TimeSpan.Zero;
    private DateTimeOffset _seekStartTime = DateTimeOffset.MinValue;
    private LyricLine? _prevLyricLine;
    private LyricLine? _currentLyricLine;
    private LyricLine? _nextLyricLine;
    private int _manualLyricOffsetIndex = -1;
    private DispatcherTimer? _manualLyricOffsetResetTimer;

    private readonly CameraService _cameraService = new();
    private string _lastSavedPhotoPath = "";
    private DispatcherTimer? _retractPhotoTimer;

    public int TargetMonitorIndex { get; set; } = -1;
    public IslandStateMachine StateMachine => _stateMachine;

    public MainWindow(
        SettingsService settingsService,
        IslandStateMachine stateMachine,
        MediaSessionService mediaService,
        SystemMonitorService systemService,
        StartupService startupService,
        ReminderService? reminderService = null,
        int targetMonitorIndex = -1)
    {
        InitializeComponent();
        TargetMonitorIndex = targetMonitorIndex;

        _settingsService = settingsService;
        _stateMachine = stateMachine;
        _mediaService = mediaService;
        _systemService = systemService;
        _startupService = startupService;
        _reminderService = reminderService ?? new ReminderService(_settingsService.Directory);

        // Timers
        _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _clockTimer.Tick += ClockTimer_Tick;

        _timelineTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
        _timelineTimer.Tick += TimelineTimer_Tick;

        _hoverTimer = new DispatcherTimer();
        _hoverTimer.Tick += HoverTimer_Tick;

        _autoCollapseTimer = new DispatcherTimer();
        _autoCollapseTimer.Tick += AutoCollapseTimer_Tick;

        _notificationTimer = new DispatcherTimer();
        _notificationTimer.Tick += NotificationTimer_Tick;

        // Events
        _stateMachine.StateChanged += OnStateMachineStateChanged;
        _mediaService.MediaChanged += OnMediaChanged;
        _systemService.MetricsUpdated += OnMetricsUpdated;
        _systemService.BatteryChanged += OnBatteryChanged;
        _systemService.NetworkChanged += OnNetworkChanged;
        _settingsService.Changed += OnSettingsChanged;

        _timerService.Tick += OnTimerTick;
        _timerService.CountdownCompleted += OnTimerCountdownCompleted;
        _weatherService.Updated += OnWeatherUpdated;
        _reminderService.Changed += OnRemindersChanged;
        _lyricsService.LyricsUpdated += OnLyricsUpdated;
        _cameraService.FrameArrived += OnCameraFrameArrived;

        Loaded += MainWindow_Loaded;
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        UpdateWindowPlacement();
        ApplySettingsToUI();

        _clockTimer.Start();
        _timelineTimer.Start();
        UpdateClockDisplay();

        // Initial media update
        UpdateMediaDisplay(_mediaService.Current);

        // Initial tools, weather, reminders
        UpdateTimerDisplay();
        UpdateWeatherDisplay(_weatherService.Current);
        RenderReminders();
        RefreshDashboardHardwareControls();

        Log.Info($"MainWindow Loaded: Left={Left}, Top={Top}, W={Width}, H={Height}, PillW={Pill.PillWidth}, PillH={Pill.PillHeight}, Opacity={Pill.Opacity}");
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        try
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd != IntPtr.Zero)
            {
                // WS_EX_TOOLWINDOW: Hide from Alt+Tab
                long exStyle = (long)NativeMethods.GetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE);
                exStyle |= NativeMethods.WS_EX_TOOLWINDOW;
                NativeMethods.SetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE, (IntPtr)exStyle);

                // Ensure topmost z-order
                NativeMethods.SetWindowPos(hwnd, NativeMethods.HWND_TOPMOST, 0, 0, 0, 0,
                    NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE);
            }

            var s = _settingsService.Current;
            _currentGeometry = IslandLayout.For(_stateMachine.State, _stateMachine.Page,
                _mediaService.Current.IsPlaying, false, s);
            IslandAnimations.SetGeometry(Pill, _currentGeometry);
        }
        catch (Exception ex)
        {
            Log.Warn("OnSourceInitialized error handled safely.", ex);
        }
    }

    public void UpdateWindowPlacement()
    {
        var s = _settingsService.Current;
        int idx = TargetMonitorIndex >= 0 ? TargetMonitorIndex : s.MonitorIndex;
        var monitor = MonitorHelper.Select(idx);
        double scale = monitor.DpiScale > 0 ? monitor.DpiScale : 1.0;

        var (hostW, hostH) = IslandLayout.HostSize(s);
        Width = hostW;
        Height = hostH;

        double monitorLeft = monitor.Bounds.Left / scale;
        double monitorTop = monitor.Bounds.Top / scale;
        double monitorWidth = monitor.Bounds.Width / scale;

        Left = monitorLeft + (monitorWidth - hostW) / 2.0;
        Top = monitorTop;

        Pill.StyleMode = s.Style;
        Pill.Margin = (s.Style == IslandStyle.Notch) ? new Thickness(0, 0, 0, 0) : new Thickness(0, s.TopOffset, 0, 0);
        Pill.SurfaceOpacity = s.Opacity;

        Log.Info($"Placement: MonitorIdx={idx}, Style={s.Style}, Left={Left}, Top={Top}, W={Width}, H={Height}, PillMargin={Pill.Margin}, SurfaceOpacity={Pill.SurfaceOpacity}");
    }

    private void ApplySettingsToUI()
    {
        var s = _settingsService.Current;
        Pill.StyleMode = s.Style;
        Pill.SurfaceOpacity = s.Opacity;

        // Update style switcher button in header
        if (s.Style == IslandStyle.Notch)
        {
            StyleSwitcherIcon.Text = "🏝️";
            StyleSwitcherText.Text = "Island";
            BtnStyleSwitcher.ToolTip = "Ganti ke mode Dynamic Island (Melayang)";
        }
        else
        {
            StyleSwitcherIcon.Text = "📱";
            StyleSwitcherText.Text = "Notch";
            BtnStyleSwitcher.ToolTip = "Ganti ke mode Notch (Menempel Bezel Atas)";
        }

        // Notch compact appearance vs Dynamic Island compact appearance
        if (s.Style == IslandStyle.Notch)
        {
            CompactClock.HorizontalAlignment = HorizontalAlignment.Center;
            CompactClock.Margin = new Thickness(0);
            Grid.SetColumn(CompactClock, 1);
            CompactSensorDot.Visibility = Visibility.Collapsed;
            CompactBatteryPanel.Visibility = Visibility.Collapsed;
        }
        else
        {
            CompactClock.HorizontalAlignment = HorizontalAlignment.Left;
            CompactClock.Margin = new Thickness(14, 0, 0, 0);
            Grid.SetColumn(CompactClock, 0);
            CompactSensorDot.Visibility = Visibility.Visible;
            CompactBatteryPanel.Visibility = Visibility.Visible;
        }

        _stateMachine.SetAvailability(
            media: s.MediaEnabled,
            system: s.SystemMonitorEnabled,
            clock: s.ClockEnabled);

        RefreshLayoutState();
    }

    private void OnSettingsChanged(string? prop)
    {
        Dispatcher.Invoke(() =>
        {
            UpdateWindowPlacement();
            ApplySettingsToUI();
            var info = _mediaService.Current;
            var pos = info.HasTimeline ? info.EstimatePosition(DateTimeOffset.Now) : TimeSpan.Zero;
            UpdateCompactLyricsDisplay(pos);
        });
    }

    // =========================================================================
    // STATE MACHINE & ANIMATION
    // =========================================================================

    private void OnStateMachineStateChanged(IslandState oldState, IslandState newState)
    {
        Dispatcher.Invoke(() =>
        {
            var s = _settingsService.Current;
            var targetGeom = IslandLayout.For(newState, _stateMachine.Page,
                _mediaService.Current.IsPlaying, _stateMachine.NotificationDetailsVisible, s);

            bool growing = IslandLayout.IsGrowing(_currentGeometry, targetGeom);
            _currentGeometry = targetGeom;

            var profile = AnimationProfile.From(s);
            IslandAnimations.AnimateGeometry(Pill, targetGeom, growing, profile);

            bool isExpanded = newState is IslandState.Expanded or IslandState.SystemInformation;

            // Turn on/off live system monitoring when system subpage is visible
            bool isSystemVisible = isExpanded && _stateMachine.Page == ExpandedPage.System;
            _systemService.SetLiveSampling(isSystemVisible);

            // Switch content layers
            if (isExpanded)
            {
                IslandAnimations.HideLayer(CompactLayer, profile);
                IslandAnimations.HideLayer(NotificationLayer, profile);
                IslandAnimations.ShowLayer(ExpandedLayer, profile);
                UpdateActiveSubpage();
            }
            else if (newState == IslandState.Notification)
            {
                IslandAnimations.HideLayer(CompactLayer, profile);
                IslandAnimations.HideLayer(ExpandedLayer, profile);
                IslandAnimations.ShowLayer(NotificationLayer, profile);
                _cameraService.Stop();
            }
            else // Idle, MusicPlaying, Hovered, TemporarilyHidden
            {
                IslandAnimations.HideLayer(ExpandedLayer, profile);
                IslandAnimations.HideLayer(NotificationLayer, profile);
                IslandAnimations.ShowLayer(CompactLayer, profile);
                _cameraService.Stop();

                bool showMusic = _mediaService.Current.IsPlaying && s.MediaEnabled;
                CompactMusicContent.Visibility = showMusic ? Visibility.Visible : Visibility.Collapsed;
                CompactIdleContent.Visibility = showMusic ? Visibility.Collapsed : Visibility.Visible;
                CompactVisualizer.SetActive(showMusic, s.ReducedMotion);
            }
        });
    }

    private void UpdateActiveSubpage()
    {
        var page = _stateMachine.Page;
        SubpageDashboard.Visibility = page == ExpandedPage.Dashboard ? Visibility.Visible : Visibility.Collapsed;
        SubpageMedia.Visibility = page == ExpandedPage.Media ? Visibility.Visible : Visibility.Collapsed;
        SubpageCamera.Visibility = page == ExpandedPage.Camera ? Visibility.Visible : Visibility.Collapsed;
        SubpageSystem.Visibility = page == ExpandedPage.System ? Visibility.Visible : Visibility.Collapsed;
        SubpageClock.Visibility = page == ExpandedPage.Clock ? Visibility.Visible : Visibility.Collapsed;
        SubpageTools.Visibility = page == ExpandedPage.Tools ? Visibility.Visible : Visibility.Collapsed;
        SubpageWeather.Visibility = page == ExpandedPage.Weather ? Visibility.Visible : Visibility.Collapsed;

        if (page == ExpandedPage.Camera)
        {
            _cameraService.Start();
            SelectCameraFilter(_cameraService.CurrentFilter, updatePreview: false);
            UpdateLiveFilterThumbnails();
            if (RetroDateReadout != null) RetroDateReadout.Text = _cameraService.CurrentDateStamp;
        }
        else
        {
            _cameraService.Stop();
        }

        if (page == ExpandedPage.Dashboard)
        {
            RefreshDashboardHardwareControls();
        }

        PageDotDashboard.Tag = page == ExpandedPage.Dashboard ? true : null;
        PageDotMedia.Tag = page == ExpandedPage.Media ? true : null;
        PageDotCamera.Tag = page == ExpandedPage.Camera ? true : null;
        PageDotSystem.Tag = page == ExpandedPage.System ? true : null;
        PageDotClock.Tag = page == ExpandedPage.Clock ? true : null;
        PageDotTools.Tag = page == ExpandedPage.Tools ? true : null;
        PageDotWeather.Tag = page == ExpandedPage.Weather ? true : null;

        var s = _settingsService.Current;
        ExpandedHeaderTitle.Text = page switch
        {
            ExpandedPage.Dashboard => s.Style == IslandStyle.Notch ? "Notch Control Center" : "Island Dashboard",
            ExpandedPage.Media => "Now Playing",
            ExpandedPage.Camera => "Retro Instant Cam 📸",
            ExpandedPage.System => "System Health",
            ExpandedPage.Clock => "Time & Date",
            ExpandedPage.Tools => "Stopwatch & Apps",
            ExpandedPage.Weather => "Weather & Notes",
            _ => "Windows Island"
        };
    }

    private void RefreshLayoutState()
    {
        OnStateMachineStateChanged(_stateMachine.State, _stateMachine.State);
    }

    // =========================================================================
    // MEDIA HANDLING
    // =========================================================================

    private void OnMediaChanged(MediaInfo info)
    {
        Dispatcher.Invoke(() =>
        {
            _stateMachine.SetMusicPlaying(info.IsPlaying && _settingsService.Current.MediaEnabled);
            UpdateMediaDisplay(info);
        });
    }

    private void UpdateMediaDisplay(MediaInfo info)
    {
        if (info.HasSession && !string.IsNullOrWhiteSpace(info.Title))
        {
            CompactTrackTitle.Text = info.Title;
            CompactArtist.Text = string.IsNullOrWhiteSpace(info.Artist) ? info.AppName : info.Artist;
            ExpandedTrackTitle.Text = info.Title;
            ExpandedArtist.Text = string.IsNullOrWhiteSpace(info.Artist) ? "Unknown Artist" : info.Artist;
            ExpandedAppName.Text = string.IsNullOrWhiteSpace(info.Album) ? info.AppName : $"{info.AppName} • {info.Album}";

            if (info.Thumbnail != null)
            {
                CompactArtwork.Source = info.Thumbnail;
                CompactArtwork.Visibility = Visibility.Visible;
                CompactMusicIconFallback.Visibility = Visibility.Collapsed;

                ExpandedArtwork.Source = info.Thumbnail;
                ExpandedArtwork.Visibility = Visibility.Visible;
                ExpandedArtworkFallback.Visibility = Visibility.Collapsed;

                var accent = ColorExtractor.GetAccent(info.Thumbnail);
                if (accent.HasValue)
                {
                    var brush = new SolidColorBrush(accent.Value);
                    brush.Freeze();
                    CompactVisualizer.BarBrush = brush;
                    ExpandedProgressBar.Foreground = brush;
                }
            }
            else
            {
                CompactArtwork.Visibility = Visibility.Collapsed;
                CompactMusicIconFallback.Visibility = Visibility.Visible;
                ExpandedArtwork.Visibility = Visibility.Collapsed;
                ExpandedArtworkFallback.Visibility = Visibility.Visible;
            }

            PlayPauseGlyph.Text = info.IsPlaying ? "\uE769" : "\uE768";
            BtnPrevTrack.IsEnabled = info.CanPrevious;
            BtnNextTrack.IsEnabled = info.CanNext;
            BtnPlayPause.IsEnabled = info.CanPlayPause;

            // ONLY reset lyrics state and query LRCLIB if the track has actually changed!
            bool trackChanged = (info.TrackKey != _lastTrackKey);
            if (trackChanged)
            {
                _lastTrackKey = info.TrackKey;
                _lastLyricIndex = -999;
                _lastLyricLine = "";
                _lastCompactLyricIndex = -999;
                _lastCompactLyricLine = "";
                _prevLyricLine = null;
                _currentLyricLine = null;
                _nextLyricLine = null;
                _manualLyricOffsetIndex = -1;
                UpdateCompactLyricsDisplay(TimeSpan.Zero);
                _ = _lyricsService.UpdateTrackAsync(info.Title, info.Artist, info.Album, info.Duration.TotalSeconds);
            }
        }
        else
        {
            CompactMusicContent.Visibility = Visibility.Collapsed;
            CompactIdleContent.Visibility = Visibility.Visible;
            if (CompactTrackInfoPanel != null) CompactTrackInfoPanel.Visibility = Visibility.Visible;
            if (CompactLyricsPanel != null) CompactLyricsPanel.Visibility = Visibility.Collapsed;
            _lastTrackKey = "";
            _lastLyricIndex = -999;
            _lastLyricLine = "";
            _lastCompactLyricIndex = -999;
            _lastCompactLyricLine = "";
            _prevLyricLine = null;
            _currentLyricLine = null;
            _nextLyricLine = null;
            _manualLyricOffsetIndex = -1;

            PlayPauseGlyph.Text = "\uE768";
            ExpandedTrackTitle.Text = "No Media Playing";
            ExpandedArtist.Text = "Start playback on Spotify, YouTube, or Edge";
            ExpandedAppName.Text = "Windows Dynamic Island";
            ExpandedArtwork.Visibility = Visibility.Collapsed;
            ExpandedArtworkFallback.Visibility = Visibility.Visible;

            if (MediaLiveLyricText != null) MediaLiveLyricText.Text = "🎵 Menunggu pemutaran musik...";
            if (MediaLyricCurrent != null) MediaLyricCurrent.Text = "Tidak ada musik diputar";
            if (MediaLyricPrev != null) MediaLyricPrev.Text = "";
            if (MediaLyricNext != null) MediaLyricNext.Text = "";
        }
    }

    private void TimelineTimer_Tick(object? sender, EventArgs e)
    {
        if (_isDraggingScrubber) return;

        var info = _mediaService.Current;
        if (!info.HasTimeline) return;

        TimeSpan pos;
        if (DateTimeOffset.Now < _seekLockUntil)
        {
            var elapsed = DateTimeOffset.Now - _seekStartTime;
            pos = _soughtTargetPosition + (info.IsPlaying ? elapsed : TimeSpan.Zero);
            var estimated = info.EstimatePosition(DateTimeOffset.Now);
            if (Math.Abs((estimated - pos).TotalSeconds) < 1.0 && elapsed.TotalMilliseconds > 400)
            {
                _seekLockUntil = DateTimeOffset.MinValue;
                pos = estimated;
            }
        }
        else
        {
            pos = info.EstimatePosition(DateTimeOffset.Now);
        }

        var dur = info.Duration;

        // 1. Expanded Media Page lyrics and timeline
        if (_stateMachine.State == IslandState.Expanded && _stateMachine.Page == ExpandedPage.Media)
        {
            if (dur.TotalSeconds > 0)
            {
                double pct = Math.Clamp(pos.TotalSeconds / dur.TotalSeconds * 100.0, 0, 100);
                ExpandedProgressBar.Value = pct;
                ExpandedPositionText.Text = FormatTime(pos);
                ExpandedDurationText.Text = FormatTime(dur);

                if (TimelineScrubberContainer != null && TimelineScrubberContainer.ActualWidth > 0)
                {
                    UpdateScrubberThumbPosition(TimelineScrubberContainer.ActualWidth * (pct / 100.0));
                }

                if (_manualLyricOffsetIndex < 0)
                {
                    UpdateLiveLyricsDisplay(pos);
                }
            }
        }

        // 2. Compact Island mode lyrics (when MusicPlaying or Hovered)
        if (_stateMachine.State == IslandState.MusicPlaying || _stateMachine.State == IslandState.Hovered)
        {
            UpdateCompactLyricsDisplay(pos);
        }
    }

    private void UpdateLiveLyricsDisplay(TimeSpan pos)
    {
        var lyrics = _lyricsService.CurrentLyrics;
        if (lyrics != null && lyrics.HasSyncedLyrics)
        {
            int rawIdx = lyrics.GetActiveIndexAt(pos);

            bool isSeek = DateTimeOffset.Now < _seekLockUntil || Math.Abs((pos - _lastEstimatedPosition).TotalSeconds) > 1.5;
            _lastEstimatedPosition = pos;

            int activeIdx = rawIdx;
            if (!isSeek && _lastLyricIndex >= 0 && rawIdx == _lastLyricIndex - 1)
            {
                activeIdx = _lastLyricIndex;
            }

            if (activeIdx != _lastLyricIndex)
            {
                _lastLyricIndex = activeIdx;
                var (curr, next, prev) = lyrics.GetLinesAtIndex(activeIdx);
                _currentLyricLine = curr;
                _nextLyricLine = next;
                _prevLyricLine = prev;

                string currText = curr?.Text ?? "";
                string prevText = prev?.Text ?? "";
                string nextText = next?.Text ?? "";

                _lastLyricLine = currText;
                AnimateLyricTransition(currText, prevText, nextText);
            }
        }
        else if (lyrics != null && !string.IsNullOrWhiteSpace(lyrics.PlainLyrics))
        {
            _currentLyricLine = null;
            _nextLyricLine = null;
            _prevLyricLine = null;
            if (MediaLiveLyricText != null && MediaLiveLyricText.Text != "🎵 Lirik lagu tersedia")
                MediaLiveLyricText.Text = "🎵 Lirik lagu tersedia";
            if (MediaLyricCurrent != null && MediaLyricCurrent.Text != lyrics.PlainLyrics)
                MediaLyricCurrent.Text = lyrics.PlainLyrics;
        }
        else
        {
            _currentLyricLine = null;
            _nextLyricLine = null;
            _prevLyricLine = null;
        }
    }

    private void AnimateLyricTransition(string curr, string prev, string next)
    {
        if (MediaLiveLyricText != null)
        {
            MediaLiveLyricText.Text = $"🎵 {curr}";
            if (CompactLyricTranslate != null)
            {
                var slide = new DoubleAnimation
                {
                    From = 6,
                    To = 0,
                    Duration = TimeSpan.FromMilliseconds(220),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };
                CompactLyricTranslate.BeginAnimation(TranslateTransform.YProperty, slide);
            }
        }

        if (MediaLyricCurrent != null)
        {
            MediaLyricCurrent.Text = curr;
            if (CurrentLyricTranslate != null)
            {
                var slide = new DoubleAnimation
                {
                    From = 6,
                    To = 0,
                    Duration = TimeSpan.FromMilliseconds(240),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };
                CurrentLyricTranslate.BeginAnimation(TranslateTransform.YProperty, slide);

                var fade = new DoubleAnimation
                {
                    From = 0.85,
                    To = 1.0,
                    Duration = TimeSpan.FromMilliseconds(200)
                };
                MediaLyricCurrent.BeginAnimation(OpacityProperty, fade);
            }
        }

        if (MediaLyricPrev != null)
        {
            MediaLyricPrev.Text = prev;
            if (PrevLyricTranslate != null)
            {
                var slide = new DoubleAnimation
                {
                    From = 4,
                    To = 0,
                    Duration = TimeSpan.FromMilliseconds(200),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };
                PrevLyricTranslate.BeginAnimation(TranslateTransform.YProperty, slide);
            }
        }

        if (MediaLyricNext != null)
        {
            MediaLyricNext.Text = next;
            if (NextLyricTranslate != null)
            {
                var slide = new DoubleAnimation
                {
                    From = 4,
                    To = 0,
                    Duration = TimeSpan.FromMilliseconds(200),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };
                NextLyricTranslate.BeginAnimation(TranslateTransform.YProperty, slide);
            }
        }
    }

    private void UpdateCompactLyricsDisplay(TimeSpan pos)
    {
        var s = _settingsService.Current;

        // If user setting is OFF: Always show default Track & Artist (as in default screenshot)
        if (!s.ShowCompactLyrics)
        {
            if (CompactTrackInfoPanel != null && CompactTrackInfoPanel.Visibility != Visibility.Visible)
                CompactTrackInfoPanel.Visibility = Visibility.Visible;
            if (CompactLyricsPanel != null && CompactLyricsPanel.Visibility != Visibility.Collapsed)
                CompactLyricsPanel.Visibility = Visibility.Collapsed;
            return;
        }

        var lyrics = _lyricsService.CurrentLyrics;
        if (lyrics != null && lyrics.HasSyncedLyrics)
        {
            int rawIdx = lyrics.GetActiveIndexAt(pos);
            bool isSeek = Math.Abs((pos - _lastEstimatedPosition).TotalSeconds) > 2.0;

            int activeIdx = rawIdx;
            if (!isSeek && _lastCompactLyricIndex >= 0 && rawIdx < _lastCompactLyricIndex)
            {
                activeIdx = _lastCompactLyricIndex;
            }

            if (activeIdx != _lastCompactLyricIndex)
            {
                _lastCompactLyricIndex = activeIdx;
                var (curr, _, _) = lyrics.GetLinesAtIndex(activeIdx);
                string currText = curr?.Text?.Trim() ?? "";

                if (!string.IsNullOrEmpty(currText))
                {
                    if (CompactTrackInfoPanel != null && CompactTrackInfoPanel.Visibility != Visibility.Collapsed)
                        CompactTrackInfoPanel.Visibility = Visibility.Collapsed;
                    if (CompactLyricsPanel != null && CompactLyricsPanel.Visibility != Visibility.Visible)
                        CompactLyricsPanel.Visibility = Visibility.Visible;

                    _lastCompactLyricLine = currText;
                    AnimateCompactLyricTransition(currText);
                    return;
                }
            }
            else if (_lastCompactLyricIndex >= 0)
            {
                if (CompactTrackInfoPanel != null && CompactTrackInfoPanel.Visibility != Visibility.Collapsed)
                    CompactTrackInfoPanel.Visibility = Visibility.Collapsed;
                if (CompactLyricsPanel != null && CompactLyricsPanel.Visibility != Visibility.Visible)
                    CompactLyricsPanel.Visibility = Visibility.Visible;
                return;
            }
        }

        // Fallback: If no synced lyric is available right now, show track title & artist
        if (CompactTrackInfoPanel != null && CompactTrackInfoPanel.Visibility != Visibility.Visible)
            CompactTrackInfoPanel.Visibility = Visibility.Visible;
        if (CompactLyricsPanel != null && CompactLyricsPanel.Visibility != Visibility.Collapsed)
            CompactLyricsPanel.Visibility = Visibility.Collapsed;
    }

    private void AnimateCompactLyricTransition(string line)
    {
        if (CompactLiveLyricText == null) return;
        CompactLiveLyricText.Text = line;

        if (CompactLiveLyricTranslate != null)
        {
            var slide = new DoubleAnimation
            {
                From = 6,
                To = 0,
                Duration = TimeSpan.FromMilliseconds(220),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            var fade = new DoubleAnimation
            {
                From = 0.85,
                To = 1.0,
                Duration = TimeSpan.FromMilliseconds(200)
            };
            CompactLiveLyricTranslate.BeginAnimation(TranslateTransform.YProperty, slide);
            CompactLiveLyricText.BeginAnimation(OpacityProperty, fade);
        }
    }

    private void OnLyricsUpdated(SongLyrics lyrics)
    {
        Dispatcher.InvokeAsync(() =>
        {
            var pos = _mediaService.Current.EstimatePosition(DateTimeOffset.Now);
            if (lyrics.HasSyncedLyrics)
            {
                UpdateLiveLyricsDisplay(pos);
                UpdateCompactLyricsDisplay(pos);
            }
            else
            {
                UpdateCompactLyricsDisplay(pos);
                if (!string.IsNullOrWhiteSpace(lyrics.PlainLyrics))
                {
                    if (MediaLiveLyricText != null) MediaLiveLyricText.Text = "🎵 Lirik lagu tersedia";
                    if (MediaLyricCurrent != null) MediaLyricCurrent.Text = lyrics.PlainLyrics;
                }
                else
                {
                    if (MediaLiveLyricText != null) MediaLiveLyricText.Text = "🎵 Lirik tidak ditemukan";
                    if (MediaLyricCurrent != null) MediaLyricCurrent.Text = "Lirik tidak ditemukan untuk lagu ini";
                    if (MediaLyricPrev != null) MediaLyricPrev.Text = "";
                    if (MediaLyricNext != null) MediaLyricNext.Text = "";
                }
            }
        });
    }

    private void ToggleLyricsMode()
    {
        _isLyricsFullMode = !_isLyricsFullMode;
        if (LyricsCompactView != null) LyricsCompactView.Visibility = _isLyricsFullMode ? Visibility.Collapsed : Visibility.Visible;
        if (LyricsFullView != null) LyricsFullView.Visibility = _isLyricsFullMode ? Visibility.Visible : Visibility.Collapsed;
        if (IconLyricsToggle != null)
        {
            IconLyricsToggle.Foreground = _isLyricsFullMode
                ? (Brush?)TryFindResource("Accent.Yellow") ?? Brushes.Gold
                : (Brush?)TryFindResource("Text.Tertiary") ?? Brushes.Gray;
        }
    }

    private void BtnToggleLyrics_Click(object sender, RoutedEventArgs e) => ToggleLyricsMode();

    private void MediaLyricsContainer_Click(object sender, MouseButtonEventArgs e) => ToggleLyricsMode();

    private static string FormatTime(TimeSpan t)
        => t.TotalHours >= 1 ? t.ToString(@"h\:mm\:ss", CultureInfo.InvariantCulture) : t.ToString(@"m\:ss", CultureInfo.InvariantCulture);

    // =========================================================================
    // TIMELINE SCRUBBER & CLICK-TO-SEEK LYRICS
    // =========================================================================

    private void TimelineScrubber_MouseEnter(object sender, MouseEventArgs e)
    {
        if (TimelineScrubberThumb != null)
        {
            var anim = new DoubleAnimation(1.0, TimeSpan.FromMilliseconds(150));
            TimelineScrubberThumb.BeginAnimation(UIElement.OpacityProperty, anim);
            ScrubberThumbScale?.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(1.2, TimeSpan.FromMilliseconds(150)));
            ScrubberThumbScale?.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(1.2, TimeSpan.FromMilliseconds(150)));
        }
    }

    private void TimelineScrubber_MouseLeave(object sender, MouseEventArgs e)
    {
        if (!_isDraggingScrubber && TimelineScrubberThumb != null)
        {
            var anim = new DoubleAnimation(0.0, TimeSpan.FromMilliseconds(200));
            TimelineScrubberThumb.BeginAnimation(UIElement.OpacityProperty, anim);
            ScrubberThumbScale?.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(0.8, TimeSpan.FromMilliseconds(200)));
            ScrubberThumbScale?.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(0.8, TimeSpan.FromMilliseconds(200)));
        }
    }

    private void TimelineScrubber_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed) return;
        var info = _mediaService.Current;
        if (!info.HasTimeline || info.Duration.TotalSeconds <= 0) return;

        _isDraggingScrubber = true;
        TimelineScrubberContainer.CaptureMouse();

        if (TimelineScrubberThumb != null)
        {
            var anim = new DoubleAnimation(1.0, TimeSpan.FromMilliseconds(100));
            TimelineScrubberThumb.BeginAnimation(UIElement.OpacityProperty, anim);
            ScrubberThumbScale?.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(1.2, TimeSpan.FromMilliseconds(100)));
            ScrubberThumbScale?.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(1.2, TimeSpan.FromMilliseconds(100)));
        }

        var mousePos = e.GetPosition(TimelineScrubberContainer);
        UpdateScrubberFromMouse(mousePos.X);
        e.Handled = true;
    }

    private void TimelineScrubber_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDraggingScrubber) return;
        var mousePos = e.GetPosition(TimelineScrubberContainer);
        UpdateScrubberFromMouse(mousePos.X);
        e.Handled = true;
    }

    private async void TimelineScrubber_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isDraggingScrubber) return;
        _isDraggingScrubber = false;
        TimelineScrubberContainer.ReleaseMouseCapture();

        var mousePos = e.GetPosition(TimelineScrubberContainer);
        UpdateScrubberFromMouse(mousePos.X);

        if (_dragTargetPosition.HasValue)
        {
            var target = _dragTargetPosition.Value;
            _soughtTargetPosition = target;
            _seekStartTime = DateTimeOffset.Now;
            _seekLockUntil = DateTimeOffset.Now.AddMilliseconds(2000);
            _lastEstimatedPosition = target;
            _manualLyricOffsetIndex = -1;
            _lastLyricIndex = -999;
            UpdateLiveLyricsDisplay(target);
            UpdateCompactLyricsDisplay(target);
            await _mediaService.ChangePlaybackPositionAsync(target);
        }

        if (!TimelineScrubberContainer.IsMouseOver && TimelineScrubberThumb != null)
        {
            var anim = new DoubleAnimation(0.0, TimeSpan.FromMilliseconds(200));
            TimelineScrubberThumb.BeginAnimation(UIElement.OpacityProperty, anim);
            ScrubberThumbScale?.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(0.8, TimeSpan.FromMilliseconds(200)));
            ScrubberThumbScale?.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(0.8, TimeSpan.FromMilliseconds(200)));
        }

        e.Handled = true;
    }

    private void UpdateScrubberFromMouse(double mouseX)
    {
        var info = _mediaService.Current;
        if (!info.HasTimeline || info.Duration.TotalSeconds <= 0) return;

        double width = TimelineScrubberContainer.ActualWidth;
        if (width <= 0) return;

        double clampedX = Math.Clamp(mouseX, 0, width);
        double ratio = clampedX / width;
        var targetPos = TimeSpan.FromSeconds(info.Duration.TotalSeconds * ratio);
        _dragTargetPosition = targetPos;

        ExpandedProgressBar.Value = ratio * 100.0;
        ExpandedPositionText.Text = FormatTime(targetPos);
        UpdateScrubberThumbPosition(clampedX);
    }

    private void UpdateScrubberThumbPosition(double x)
    {
        if (TimelineScrubberThumb == null || TimelineScrubberContainer == null) return;
        double width = TimelineScrubberContainer.ActualWidth;
        if (width <= 0) return;

        double clampedX = Math.Clamp(x, 0, width);
        double thumbLeft = Math.Clamp(clampedX - 5.0, 0, Math.Max(0, width - 10.0));
        Canvas.SetLeft(TimelineScrubberThumb, thumbLeft);
    }

    private async void SeekToLyricTimestamp(TimeSpan ts)
    {
        var info = _mediaService.Current;
        if (!info.HasTimeline || info.Duration.TotalSeconds <= 0) return;

        Log.Info($"SeekToLyricTimestamp: Seeking to {ts}");
        _soughtTargetPosition = ts;
        _seekStartTime = DateTimeOffset.Now;
        _seekLockUntil = DateTimeOffset.Now.AddMilliseconds(2000);
        _lastEstimatedPosition = ts;
        _manualLyricOffsetIndex = -1;
        _lastLyricIndex = -999;

        double pct = Math.Clamp(ts.TotalSeconds / info.Duration.TotalSeconds * 100.0, 0, 100);
        ExpandedProgressBar.Value = pct;
        ExpandedPositionText.Text = FormatTime(ts);
        if (TimelineScrubberContainer != null && TimelineScrubberContainer.ActualWidth > 0)
        {
            UpdateScrubberThumbPosition(TimelineScrubberContainer.ActualWidth * (pct / 100.0));
        }

        UpdateLiveLyricsDisplay(ts);
        UpdateCompactLyricsDisplay(ts);
        await _mediaService.ChangePlaybackPositionAsync(ts);
    }

    private void MediaLyricPrev_Click(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        if (_prevLyricLine != null)
        {
            SeekToLyricTimestamp(_prevLyricLine.Timestamp);
        }
        else
        {
            _ = _mediaService.SeekRelativeAsync(TimeSpan.FromSeconds(-10));
        }
    }

    private void MediaLyricCurrent_Click(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        if (_currentLyricLine != null)
        {
            SeekToLyricTimestamp(_currentLyricLine.Timestamp);
        }
    }

    private void MediaLyricNext_Click(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        if (_nextLyricLine != null)
        {
            SeekToLyricTimestamp(_nextLyricLine.Timestamp);
        }
        else
        {
            _ = _mediaService.SeekRelativeAsync(TimeSpan.FromSeconds(10));
        }
    }

    private void CompactLyric_Click(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        var lyrics = _lyricsService.CurrentLyrics;
        if (lyrics != null && lyrics.HasSyncedLyrics)
        {
            var info = _mediaService.Current;
            var pos = info.EstimatePosition(DateTimeOffset.Now);
            int idx = lyrics.GetActiveIndexAt(pos);
            var (curr, _, _) = lyrics.GetLinesAtIndex(idx);
            if (curr != null)
            {
                SeekToLyricTimestamp(curr.Timestamp);
                return;
            }
        }
        ToggleLyricsMode();
    }

    private void MediaLyricsContainer_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        var lyrics = _lyricsService.CurrentLyrics;
        if (lyrics == null || !lyrics.HasSyncedLyrics || lyrics.Lines.Count == 0) return;

        e.Handled = true;

        if (_manualLyricOffsetIndex < 0)
        {
            var info = _mediaService.Current;
            var pos = info.EstimatePosition(DateTimeOffset.Now);
            _manualLyricOffsetIndex = Math.Max(0, lyrics.GetActiveIndexAt(pos));
        }

        if (e.Delta < 0)
        {
            _manualLyricOffsetIndex = Math.Min(lyrics.Lines.Count - 1, _manualLyricOffsetIndex + 1);
        }
        else if (e.Delta > 0)
        {
            _manualLyricOffsetIndex = Math.Max(0, _manualLyricOffsetIndex - 1);
        }

        var (curr, next, prev) = lyrics.GetLinesAtIndex(_manualLyricOffsetIndex);
        _currentLyricLine = curr;
        _nextLyricLine = next;
        _prevLyricLine = prev;

        AnimateLyricTransition(curr?.Text ?? "", prev?.Text ?? "", next?.Text ?? "");

        if (_manualLyricOffsetResetTimer == null)
        {
            _manualLyricOffsetResetTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
            _manualLyricOffsetResetTimer.Tick += (s, args) =>
            {
                _manualLyricOffsetResetTimer.Stop();
                _manualLyricOffsetIndex = -1;
                _lastLyricIndex = -999;
            };
        }
        _manualLyricOffsetResetTimer.Stop();
        _manualLyricOffsetResetTimer.Start();
    }

    // =========================================================================
    // SYSTEM & METRICS HANDLING
    // =========================================================================

    private void OnMetricsUpdated(SystemMetrics m)
    {
        CpuGauge.Value = m.CpuPercent;
        CpuText.Text = $"{Math.Round(m.CpuPercent)}%";

        RamGauge.Value = m.MemoryPercent;
        RamText.Text = $"{Math.Round(m.MemoryPercent)}%";
    }

    private void OnBatteryChanged(BatteryStatus oldBat, BatteryStatus newBat)
    {
        if (newBat.HasBattery)
        {
            CompactBattery.Percent = newBat.Percent;
            CompactBatteryText.Text = $"{newBat.Percent}%";
            SystemBatteryGlyph.Percent = newBat.Percent;
            SystemBatteryPercent.Text = $"{newBat.Percent}%";
            SystemBatteryStatus.Text = newBat.IsCharging ? "Charging" : (newBat.IsPluggedIn ? "Plugged In" : "On Battery");

            // Automatic system notifications for power events
            if (_settingsService.Current.NotificationsEnabled && oldBat.HasBattery)
            {
                if (!oldBat.IsCharging && newBat.IsCharging)
                {
                    ShowNotification(new NotificationInfo
                    {
                        Kind = NotificationKind.Battery,
                        AppName = "Power",
                        Title = "Power Connected",
                        Message = $"Charging • {newBat.Percent}%",
                        IconGlyph = "⚡",
                        AccentColor = "#10B981"
                    });
                }
                else if (oldBat.IsPluggedIn && !newBat.IsPluggedIn)
                {
                    ShowNotification(new NotificationInfo
                    {
                        Kind = NotificationKind.Battery,
                        AppName = "Battery",
                        Title = "Power Disconnected",
                        Message = $"On Battery • {newBat.Percent}%",
                        IconGlyph = "🔋",
                        AccentColor = "#F59E0B"
                    });
                }
                else if (oldBat.Percent > 20 && newBat.Percent <= 20 && !newBat.IsCharging)
                {
                    ShowNotification(new NotificationInfo
                    {
                        Kind = NotificationKind.Battery,
                        AppName = "Battery Warning",
                        Title = "Low Battery",
                        Message = $"{newBat.Percent}% remaining • Connect power",
                        IconGlyph = "🪫",
                        AccentColor = "#EF4444"
                    });
                }
            }
        }
        else
        {
            CompactBattery.Percent = 100;
            CompactBatteryText.Text = "AC";
            SystemBatteryGlyph.Percent = 100;
            SystemBatteryPercent.Text = "100%";
            SystemBatteryStatus.Text = "Desktop AC Power";
        }
    }

    private void OnNetworkChanged(NetworkStatus oldNet, NetworkStatus newNet)
    {
        if (newNet.IsConnected)
        {
            SystemNetworkIcon.Text = newNet.Kind == NetworkKind.Ethernet ? "\uE839" : "\uE701";
            SystemNetworkStatus.Text = "Connected";
            SystemNetworkName.Text = string.IsNullOrWhiteSpace(newNet.Name) ? "Internet" : newNet.Name;

            if (_settingsService.Current.NotificationsEnabled && !oldNet.IsConnected)
            {
                ShowNotification(new NotificationInfo
                {
                    Kind = NotificationKind.Network,
                    AppName = "Network",
                    Title = "Network Connected",
                    Message = $"Connected to {(string.IsNullOrWhiteSpace(newNet.Name) ? "Internet" : newNet.Name)}",
                    IconGlyph = "📶",
                    AccentColor = "#3B82F6"
                });
            }
        }
        else
        {
            SystemNetworkIcon.Text = "\uEB55";
            SystemNetworkStatus.Text = "Disconnected";
            SystemNetworkName.Text = "No Internet";

            if (_settingsService.Current.NotificationsEnabled && oldNet.IsConnected)
            {
                ShowNotification(new NotificationInfo
                {
                    Kind = NotificationKind.Network,
                    AppName = "Network",
                    Title = "Network Disconnected",
                    Message = "Internet connection lost",
                    IconGlyph = "⚠️",
                    AccentColor = "#EF4444"
                });
            }
        }
    }

    // =========================================================================
    // CLOCK & FULLSCREEN TICK
    // =========================================================================

    private void ClockTimer_Tick(object? sender, EventArgs e)
    {
        UpdateClockDisplay();
        UpdateDayProgress();

        // Check Fullscreen state if configured
        var s = _settingsService.Current;
        if (s.HideInFullscreen && NativeMethods.SHQueryUserNotificationState(out var qState) == 0)
        {
            bool fullscreen = qState == NativeMethods.QUERY_USER_NOTIFICATION_STATE.QUNS_RUNNING_D3D_FULL_SCREEN;
            _stateMachine.SetFullscreenHidden(fullscreen);
        }
        else
        {
            _stateMachine.SetFullscreenHidden(false);
        }
    }

    private void UpdateDayProgress()
    {
        var now = DateTime.Now;
        double minutesPassed = now.Hour * 60 + now.Minute;
        double dayPct = Math.Clamp(minutesPassed / 1440.0 * 100.0, 0, 100);

        DashDayPercent.Text = $"{(int)dayPct}%";
        DashDayProgressBar.Value = dayPct;

        var midnight = DateTime.Today.AddDays(1);
        var remaining = midnight - now;
        DashBedtimeCountdown.Text = $"{(int)remaining.TotalHours}h {remaining.Minutes}m until bedtime";
        DashDigitalClock.Text = now.ToString("HH:mm  dd MMM", CultureInfo.InvariantCulture);
    }

    private void UpdateClockDisplay()
    {
        var now = DateTime.Now;
        var s = _settingsService.Current;

        string timeFmt = s.ClockFormat switch
        {
            ClockFormat.TwelveHour => "hh:mm tt",
            ClockFormat.TwentyFourHour => "HH:mm",
            _ => CultureInfo.CurrentCulture.DateTimeFormat.ShortTimePattern
        };

        CompactClock.Text = now.ToString(timeFmt, CultureInfo.CurrentCulture);
        ExpandedClockTime.Text = now.ToString("HH:mm:ss", CultureInfo.CurrentCulture);
        ExpandedClockDate.Text = now.ToString("dddd, d MMMM yyyy", CultureInfo.CurrentCulture);
    }

    // =========================================================================
    // NOTIFICATIONS
    // =========================================================================

    public void ShowNotification(NotificationInfo n)
    {
        if (!_settingsService.Current.NotificationsEnabled) return;

        NotificationAppName.Text = (string.IsNullOrWhiteSpace(n.AppName) ? "NOTIFICATION" : n.AppName).ToUpperInvariant();
        NotificationTime.Text = "now";
        NotificationTitle.Text = string.IsNullOrWhiteSpace(n.Title) ? (n.AppName ?? "Notification") : n.Title;
        NotificationMessage.Text = n.Message ?? "";
        NotificationIcon.Text = string.IsNullOrWhiteSpace(n.IconGlyph) ? "🔔" : n.IconGlyph;

        if (!string.IsNullOrWhiteSpace(n.AccentColor))
        {
            try
            {
                var brush = (Brush)new BrushConverter().ConvertFromString(n.AccentColor)!;
                NotificationBadgeBorder.Background = brush;
            }
            catch
            {
                NotificationBadgeBorder.Background = (Brush)FindResource("Accent.Blue");
            }
        }

        if (Visibility != Visibility.Visible)
        {
            Visibility = Visibility.Visible;
        }

        _stateMachine.SetNotificationActive(true);

        var s = _settingsService.Current;
        int seconds = n.Timeout.HasValue ? (int)n.Timeout.Value.TotalSeconds : s.NotificationTimeoutSeconds;
        _notificationTimer.Stop();
        _notificationTimer.Interval = TimeSpan.FromSeconds(Math.Max(1, seconds));
        _notificationTimer.Start();
    }

    private void NotificationTimer_Tick(object? sender, EventArgs e)
    {
        _notificationTimer.Stop();
        _stateMachine.SetNotificationActive(false);
    }

    private void BtnDismissNotification_Click(object sender, RoutedEventArgs e)
    {
        _notificationTimer.Stop();
        _stateMachine.SetNotificationActive(false);
    }

    // =========================================================================
    // MOUSE & GESTURE INTERACTION
    // =========================================================================

    private void Pill_MouseEnter(object sender, MouseEventArgs e)
    {
        _autoCollapseTimer.Stop();
        var s = _settingsService.Current;

        if (s.ExpandOnHover && !_stateMachine.IsExpandedByUser)
        {
            _hoverTimer.Stop();
            _hoverTimer.Interval = TimeSpan.FromMilliseconds(Math.Max(50, s.HoverDelayMs));
            _hoverTimer.Start();
        }
        else
        {
            _stateMachine.SetHovered(true);
        }
    }

    private void HoverTimer_Tick(object? sender, EventArgs e)
    {
        _hoverTimer.Stop();
        _stateMachine.SetExpanded(true);
    }

    private void Pill_MouseLeave(object sender, MouseEventArgs e)
    {
        _hoverTimer.Stop();
        _stateMachine.SetHovered(false);

        var s = _settingsService.Current;
        if (_stateMachine.IsExpandedByUser && s.AutoCollapseSeconds > 0)
        {
            _autoCollapseTimer.Stop();
            _autoCollapseTimer.Interval = TimeSpan.FromSeconds(s.AutoCollapseSeconds);
            _autoCollapseTimer.Start();
        }
    }

    private void AutoCollapseTimer_Tick(object? sender, EventArgs e)
    {
        _autoCollapseTimer.Stop();
        _stateMachine.SetExpanded(false);
    }

    private void Pill_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        // If already expanded, clicking inside content should never collapse it!
        if (_stateMachine.IsExpandedByUser || _stateMachine.State == IslandState.Expanded)
        {
            return;
        }

        if (_settingsService.Current.ExpandOnClick)
        {
            _hoverTimer.Stop();
            _autoCollapseTimer.Stop();
            _stateMachine.ToggleExpanded();
        }
    }

    private void Pill_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        var menu = new ContextMenu();

        var toggleItem = new MenuItem { Header = _stateMachine.IsExpandedByUser ? "Collapse Island" : "Expand Island" };
        toggleItem.Click += (_, _) => _stateMachine.ToggleExpanded();
        menu.Items.Add(toggleItem);

        var pageMenu = new MenuItem { Header = "Switch Page" };

        var dashItem = new MenuItem { Header = "Control Dashboard" };
        dashItem.Click += (_, _) => { _stateMachine.SetExpanded(true); _stateMachine.SelectPage(ExpandedPage.Dashboard); };
        pageMenu.Items.Add(dashItem);

        var mediaItem = new MenuItem { Header = "Media Player" };
        mediaItem.Click += (_, _) => { _stateMachine.SetExpanded(true); _stateMachine.SelectPage(ExpandedPage.Media); };
        pageMenu.Items.Add(mediaItem);

        var cameraItem = new MenuItem { Header = "📷 Dashcam Viewfinder & Foto" };
        cameraItem.Click += (_, _) => { _stateMachine.SetExpanded(true); _stateMachine.SelectPage(ExpandedPage.Camera); };
        pageMenu.Items.Add(cameraItem);

        var sysItem = new MenuItem { Header = "System Health" };
        sysItem.Click += (_, _) => { _stateMachine.SetExpanded(true); _stateMachine.SelectPage(ExpandedPage.System); };
        pageMenu.Items.Add(sysItem);

        var clockItem = new MenuItem { Header = "Clock & Shortcuts" };
        clockItem.Click += (_, _) => { _stateMachine.SetExpanded(true); _stateMachine.SelectPage(ExpandedPage.Clock); };
        pageMenu.Items.Add(clockItem);

        var toolsItem = new MenuItem { Header = "Stopwatch & Apps" };
        toolsItem.Click += (_, _) => { _stateMachine.SetExpanded(true); _stateMachine.SelectPage(ExpandedPage.Tools); };
        pageMenu.Items.Add(toolsItem);

        var weatherItem = new MenuItem { Header = "Weather & Reminders" };
        weatherItem.Click += (_, _) => { _stateMachine.SetExpanded(true); _stateMachine.SelectPage(ExpandedPage.Weather); };
        pageMenu.Items.Add(weatherItem);

        menu.Items.Add(pageMenu);

        var styleToggleItem = new MenuItem
        {
            Header = _settingsService.Current.Style == IslandStyle.Notch
                ? "Switch Style: Dynamic Island (Floating)"
                : "Switch Style: Notch (Top Bezel Attached)"
        };
        styleToggleItem.Click += (_, _) =>
        {
            var s = _settingsService.Current;
            s.Style = s.Style == IslandStyle.DynamicIsland ? IslandStyle.Notch : IslandStyle.DynamicIsland;
            _settingsService.NotifyChanged(nameof(AppSettings.Style));
        };
        menu.Items.Add(styleToggleItem);

        menu.Items.Add(new Separator());

        var settingsItem = new MenuItem { Header = "Settings..." };
        settingsItem.Click += (_, _) => OpenSettings();
        menu.Items.Add(settingsItem);

        var hideItem = new MenuItem { Header = "Hide to System Tray" };
        hideItem.Click += (_, _) => ToggleVisibility();
        menu.Items.Add(hideItem);

        menu.Items.Add(new Separator());

        var exitItem = new MenuItem { Header = "Exit Windows Dynamic Island" };
        exitItem.Click += (_, _) => Application.Current.Shutdown();
        menu.Items.Add(exitItem);

        menu.IsOpen = true;
    }

    // =========================================================================
    // UI BUTTON ACTIONS
    // =========================================================================

    private void BtnStyleSwitcher_Click(object sender, RoutedEventArgs e)
    {
        var s = _settingsService.Current;
        s.Style = s.Style == IslandStyle.DynamicIsland ? IslandStyle.Notch : IslandStyle.DynamicIsland;
        _settingsService.NotifyChanged(nameof(AppSettings.Style));
    }

    private void PageDotDashboard_Click(object sender, RoutedEventArgs e)
        => _stateMachine.SelectPage(ExpandedPage.Dashboard);

    private void DashWeatherCard_Click(object sender, MouseButtonEventArgs e)
        => _stateMachine.SelectPage(ExpandedPage.Weather);

    private void BtnSettings_Click(object sender, RoutedEventArgs e)
        => OpenSettings();

    // Quick Toggles
    private void BtnToggleWifi_Click(object sender, RoutedEventArgs e)
    {
        try { Process.Start(new ProcessStartInfo("ms-settings:network-wifi") { UseShellExecute = true }); }
        catch { try { Process.Start(new ProcessStartInfo("ms-settings:network") { UseShellExecute = true }); } catch { } }
    }

    private void BtnToggleBluetooth_Click(object sender, RoutedEventArgs e)
    {
        try { Process.Start(new ProcessStartInfo("ms-settings:bluetooth") { UseShellExecute = true }); }
        catch (Exception ex) { Log.Warn("Could not open bluetooth settings.", ex); }
    }

    private void BtnToggleNightLight_Click(object sender, RoutedEventArgs e)
    {
        try { Process.Start(new ProcessStartInfo("ms-settings:nightlight") { UseShellExecute = true }); }
        catch { try { Process.Start(new ProcessStartInfo("ms-settings:display") { UseShellExecute = true }); } catch { } }
    }

    private void BtnToggleHotspot_Click(object sender, RoutedEventArgs e)
    {
        try { Process.Start(new ProcessStartInfo("ms-settings:network-mobilehotspot") { UseShellExecute = true }); }
        catch (Exception ex) { Log.Warn("Could not open mobile hotspot settings.", ex); }
    }

    private void BtnToggleMute_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            NativeMethods.keybd_event(NativeMethods.VK_VOLUME_MUTE, 0, 0, UIntPtr.Zero);
            NativeMethods.keybd_event(NativeMethods.VK_VOLUME_MUTE, 0, NativeMethods.KEYEVENTF_KEYUP, UIntPtr.Zero);
        }
        catch (Exception ex) { Log.Warn("Could not toggle mute.", ex); }
    }

    private void BtnToggleVolume_Click(object sender, RoutedEventArgs e)
    {
        try { Process.Start(new ProcessStartInfo("sndvol.exe") { UseShellExecute = true }); }
        catch { try { Process.Start(new ProcessStartInfo("ms-settings:sound") { UseShellExecute = true }); } catch { } }
    }

    private void BtnToggleSnip_Click(object sender, RoutedEventArgs e)
    {
        try { Process.Start(new ProcessStartInfo("ms-screenclip:") { UseShellExecute = true }); }
        catch { try { Process.Start(new ProcessStartInfo("snippingtool.exe") { UseShellExecute = true }); } catch { } }
    }

    private void BtnToggleLock_Click(object sender, RoutedEventArgs e)
    {
        try { NativeMethods.LockWorkStation(); }
        catch (Exception ex) { Log.Warn("Could not lock workstation.", ex); }
    }

    // Quick Apps
    private void BtnAppExplorer_Click(object sender, RoutedEventArgs e)
    {
        try { Process.Start(new ProcessStartInfo("explorer.exe") { UseShellExecute = true }); }
        catch (Exception ex) { Log.Warn("Could not start Explorer.", ex); }
    }

    private void BtnAppTerminal_Click(object sender, RoutedEventArgs e)
    {
        try { Process.Start(new ProcessStartInfo("wt.exe") { UseShellExecute = true }); }
        catch { try { Process.Start(new ProcessStartInfo("powershell.exe") { UseShellExecute = true }); } catch { } }
    }

    private void PageDotMedia_Click(object sender, RoutedEventArgs e)
        => _stateMachine.SelectPage(ExpandedPage.Media);

    private void PageDotCamera_Click(object sender, RoutedEventArgs e)
        => _stateMachine.SelectPage(ExpandedPage.Camera);

    private void BtnToggleDashcam_Click(object sender, RoutedEventArgs e)
    {
        _stateMachine.SetExpanded(true);
        _stateMachine.SelectPage(ExpandedPage.Camera);
    }

    private int _filterThumbCycle;

    private void OnCameraFrameArrived(System.Windows.Media.Imaging.BitmapSource bmp)
    {
        Dispatcher.InvokeAsync(() =>
        {
            if (_stateMachine.State == IslandState.Expanded && _stateMachine.Page == ExpandedPage.Camera)
            {
                if (CameraPreviewImage != null) CameraPreviewImage.Source = bmp;
                if (CameraPreviewBlurImage != null) CameraPreviewBlurImage.Source = bmp;
                if (RetroDateReadout != null) RetroDateReadout.Text = _cameraService.CurrentDateStamp;

                UpdateLiveFilterThumbnailsSmooth();
            }
        });
    }

    private void UpdateLiveFilterThumbnailsSmooth()
    {
        try
        {
            // Smooth round-robin update: update 2 thumbnails per frame for silky-smooth 60fps UI
            switch (_filterThumbCycle % 3)
            {
                case 0:
                    if (FilterThumb_Normal != null) FilterThumb_Normal.Source = _cameraService.GenerateFilterThumbnail(RetroFilter.Normal, 56, 36);
                    if (FilterThumb_Warm90s != null) FilterThumb_Warm90s.Source = _cameraService.GenerateFilterThumbnail(RetroFilter.Warm90s, 56, 36);
                    break;
                case 1:
                    if (FilterThumb_Cyberpunk != null) FilterThumb_Cyberpunk.Source = _cameraService.GenerateFilterThumbnail(RetroFilter.Cyberpunk, 56, 36);
                    if (FilterThumb_Monochrome != null) FilterThumb_Monochrome.Source = _cameraService.GenerateFilterThumbnail(RetroFilter.Monochrome, 56, 36);
                    break;
                case 2:
                    if (FilterThumb_Sepia != null) FilterThumb_Sepia.Source = _cameraService.GenerateFilterThumbnail(RetroFilter.Sepia, 56, 36);
                    if (FilterThumb_Vibrant != null) FilterThumb_Vibrant.Source = _cameraService.GenerateFilterThumbnail(RetroFilter.Vibrant, 56, 36);
                    break;
            }
            _filterThumbCycle++;
        }
        catch { }
    }

    private void SelectCameraFilter(RetroFilter filter, bool updatePreview = true)
    {
        _cameraService.CurrentFilter = filter;

        if (FilterLabelText != null) FilterLabelText.Text = CameraService.GetFilterDisplayName(filter);
        if (FilterIconEmoji != null) FilterIconEmoji.Text = CameraService.GetFilterEmoji(filter);
        if (CameraModeText != null) CameraModeText.Text = $" • {CameraService.GetFilterDisplayName(filter).ToUpperInvariant()}";

        // Update active highlight on macOS style filter cards
        HighlightFilterCard(CardFilter_Normal, filter == RetroFilter.Normal);
        HighlightFilterCard(CardFilter_Warm90s, filter == RetroFilter.Warm90s);
        HighlightFilterCard(CardFilter_Cyberpunk, filter == RetroFilter.Cyberpunk);
        HighlightFilterCard(CardFilter_Monochrome, filter == RetroFilter.Monochrome);
        HighlightFilterCard(CardFilter_Sepia, filter == RetroFilter.Sepia);
        HighlightFilterCard(CardFilter_Vibrant, filter == RetroFilter.Vibrant);

        if (updatePreview)
        {
            UpdateLiveFilterThumbnails();
        }
    }

    private static void HighlightFilterCard(Border? card, bool isSelected)
    {
        if (card == null) return;
        if (isSelected)
        {
            card.BorderBrush = new SolidColorBrush(Color.FromRgb(59, 130, 246)); // Apple Accent Blue
            card.BorderThickness = new Thickness(2.0);
            card.Opacity = 1.0;
            card.Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                Color = Color.FromRgb(59, 130, 246),
                BlurRadius = 8,
                ShadowDepth = 0,
                Opacity = 0.65
            };
        }
        else
        {
            card.BorderBrush = new SolidColorBrush(Color.FromArgb(50, 255, 255, 255));
            card.BorderThickness = new Thickness(1.0);
            card.Opacity = 0.75;
            card.Effect = null;
        }
    }

    private void FilterPresetCard_Click(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        if (sender is Border card && card.Tag is string tag)
        {
            if (Enum.TryParse<RetroFilter>(tag, out var filter))
            {
                SelectCameraFilter(filter, updatePreview: true);
            }
        }
    }

    private void UpdateLiveFilterThumbnails()
    {
        try
        {
            if (FilterThumb_Normal != null) FilterThumb_Normal.Source = _cameraService.GenerateFilterThumbnail(RetroFilter.Normal, 56, 36);
            if (FilterThumb_Warm90s != null) FilterThumb_Warm90s.Source = _cameraService.GenerateFilterThumbnail(RetroFilter.Warm90s, 56, 36);
            if (FilterThumb_Cyberpunk != null) FilterThumb_Cyberpunk.Source = _cameraService.GenerateFilterThumbnail(RetroFilter.Cyberpunk, 56, 36);
            if (FilterThumb_Monochrome != null) FilterThumb_Monochrome.Source = _cameraService.GenerateFilterThumbnail(RetroFilter.Monochrome, 56, 36);
            if (FilterThumb_Sepia != null) FilterThumb_Sepia.Source = _cameraService.GenerateFilterThumbnail(RetroFilter.Sepia, 56, 36);
            if (FilterThumb_Vibrant != null) FilterThumb_Vibrant.Source = _cameraService.GenerateFilterThumbnail(RetroFilter.Vibrant, 56, 36);
        }
        catch { }
    }

    private void BtnToggleCameraFilter_Click(object sender, RoutedEventArgs e)
    {
        _cameraService.CycleFilter();
        SelectCameraFilter(_cameraService.CurrentFilter, updatePreview: true);
    }

    private async void BtnCaptureRetroPhoto_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // 1. Camera Flash Animation on viewfinder
            if (ViewfinderFlash != null)
            {
                var flashAnim = new DoubleAnimation
                {
                    From = 0.95,
                    To = 0.0,
                    Duration = TimeSpan.FromMilliseconds(220),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };
                ViewfinderFlash.BeginAnimation(OpacityProperty, flashAnim);
            }

            // 2. Capture and auto-save Polaroid photo
            var photo = await _cameraService.CapturePolaroidPhotoAsync();
            string savedPath = _cameraService.SavePhotoToDevice(photo);
            _lastSavedPhotoPath = savedPath;

            // 3. Refresh recent prints strip below viewfinder
            RefreshRecentPrints();

            // 4. Eject Printed Polaroid Photo Card Animation (Sliding downwards out of the Dynamic Island slot!)
            if (PrintedPhotoCard != null && PrintedPhotoImage != null)
            {
                // Reset any running timer and remove existing animation clocks so animation can run reliably every single time!
                _retractPhotoTimer?.Stop();
                PrintedPhotoCard.BeginAnimation(OpacityProperty, null);
                PrintedPhotoTranslate.BeginAnimation(TranslateTransform.YProperty, null);
                if (PrintedPhotoBlur != null)
                {
                    PrintedPhotoBlur.BeginAnimation(System.Windows.Media.Effects.BlurEffect.RadiusProperty, null);
                }
                if (PrintedPhotoDevelopingSheen != null)
                {
                    PrintedPhotoDevelopingSheen.BeginAnimation(OpacityProperty, null);
                }

                PrintedPhotoImage.Source = _cameraService.LastCapturedPhotoOnly ?? photo;
                if (PrintedPhotoTimestamp != null) PrintedPhotoTimestamp.Text = $"{_cameraService.CurrentDateStamp} • RETRO CAM";
                if (PrintedPhotoPathText != null)
                {
                    PrintedPhotoPathText.Text = "⏳ Developing film...";
                    PrintedPhotoPathText.Foreground = new SolidColorBrush(Color.FromRgb(245, 158, 11)); // Amber
                }

                var s = _settingsService.Current;
                double pillTop = (s.Style == IslandStyle.Notch) ? 0 : s.TopOffset;
                double pillBottom = pillTop + Pill.PillHeight;

                const double cardHeight = 250;
                double startY = pillBottom - cardHeight + 8; // Tucked completely behind the island bottom bezel!
                double targetY = pillBottom + 6; // Slides down emerging from bottom bezel slot!

                // Reset visual base state
                PrintedPhotoCard.Opacity = 1.0;
                PrintedPhotoTranslate.Y = startY;
                PrintedPhotoCard.Visibility = Visibility.Visible;

                // Initial blur & chemical sheen on film before development
                if (PrintedPhotoBlur != null)
                {
                    PrintedPhotoBlur.Radius = 24.0; // Starts blurry
                }
                if (PrintedPhotoDevelopingSheen != null)
                {
                    PrintedPhotoDevelopingSheen.Opacity = 0.65; // Milky undeveloped film look
                }

                // Realistic mechanical printer ejection: smooth continuous feed out of the bottom slot
                var ejectAnim = new DoubleAnimationUsingKeyFrames { FillBehavior = FillBehavior.HoldEnd };
                ejectAnim.KeyFrames.Add(new LinearDoubleKeyFrame(startY, KeyTime.FromTimeSpan(TimeSpan.Zero)));
                // Initial motor bite & paper grip
                ejectAnim.KeyFrames.Add(new SplineDoubleKeyFrame(startY + (targetY - startY) * 0.18, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(200)), new KeySpline(0.2, 0.0, 0.4, 1.0)));
                // Continuous smooth extrusion from slot
                ejectAnim.KeyFrames.Add(new SplineDoubleKeyFrame(startY + (targetY - startY) * 0.85, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(850)), new KeySpline(0.1, 0.2, 0.3, 1.0)));
                // Gentle decelerating settle
                ejectAnim.KeyFrames.Add(new SplineDoubleKeyFrame(targetY, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(1150)), new KeySpline(0.1, 0.8, 0.2, 1.0)));

                PrintedPhotoTranslate.BeginAnimation(TranslateTransform.YProperty, ejectAnim);

                // Film development animation: blur fades from 24 down to 0, sheen fades to 0
                if (PrintedPhotoBlur != null)
                {
                    var blurAnim = new DoubleAnimation
                    {
                        From = 24.0,
                        To = 0.0,
                        Duration = TimeSpan.FromMilliseconds(1200),
                        BeginTime = TimeSpan.FromMilliseconds(250),
                        EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
                        FillBehavior = FillBehavior.HoldEnd
                    };
                    blurAnim.Completed += (sAnim, eAnim) =>
                    {
                        if (PrintedPhotoPathText != null)
                        {
                            PrintedPhotoPathText.Text = "✓ Auto-saved to Pictures";
                            PrintedPhotoPathText.Foreground = new SolidColorBrush(Color.FromRgb(22, 163, 74));
                        }
                    };
                    PrintedPhotoBlur.BeginAnimation(System.Windows.Media.Effects.BlurEffect.RadiusProperty, blurAnim);
                }

                if (PrintedPhotoDevelopingSheen != null)
                {
                    var sheenAnim = new DoubleAnimation
                    {
                        From = 0.60,
                        To = 0.0,
                        Duration = TimeSpan.FromMilliseconds(1000),
                        BeginTime = TimeSpan.FromMilliseconds(250),
                        EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
                        FillBehavior = FillBehavior.HoldEnd
                    };
                    PrintedPhotoDevelopingSheen.BeginAnimation(OpacityProperty, sheenAnim);
                }

                // Auto-retract preview after 3 seconds as requested by the user
                _retractPhotoTimer?.Stop();
                _retractPhotoTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
                _retractPhotoTimer.Tick += (st, ev) =>
                {
                    _retractPhotoTimer.Stop();
                    DismissPrintedPhoto();
                };
                _retractPhotoTimer.Start();
            }
        }
        catch (Exception ex)
        {
            Log.Warn("Failed to capture retro photo", ex);
        }
    }

    private void BtnCaptureDashcamPhoto_Click(object sender, RoutedEventArgs e)
    {
        BtnCaptureRetroPhoto_Click(sender, e);
    }

    private void RefreshRecentPrints()
    {
        // Recent prints are auto-saved to Pictures\WindowsDynamicIsland
        // and accessible via the Galeri button or the sliding Polaroid ejection animation.
    }

    private void DismissPrintedPhoto()
    {
        if (PrintedPhotoCard == null || PrintedPhotoCard.Visibility != Visibility.Visible) return;
        _retractPhotoTimer?.Stop();

        var fade = new DoubleAnimation
        {
            From = PrintedPhotoCard.Opacity,
            To = 0.0,
            Duration = TimeSpan.FromMilliseconds(250),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
        };
        fade.Completed += (_, _) =>
        {
            PrintedPhotoCard.Visibility = Visibility.Collapsed;
            PrintedPhotoCard.BeginAnimation(OpacityProperty, null);
            PrintedPhotoCard.Opacity = 0.0;
            PrintedPhotoTranslate.BeginAnimation(TranslateTransform.YProperty, null);
            if (PrintedPhotoBlur != null) PrintedPhotoBlur.BeginAnimation(System.Windows.Media.Effects.BlurEffect.RadiusProperty, null);
            if (PrintedPhotoDevelopingSheen != null) PrintedPhotoDevelopingSheen.BeginAnimation(OpacityProperty, null);
        };
        PrintedPhotoCard.BeginAnimation(OpacityProperty, fade);
    }

    private void PrintedPhotoCard_Click(object sender, MouseButtonEventArgs e) => OpenLastSavedPhoto();

    private void BtnOpenSavedPhoto_Click(object sender, RoutedEventArgs e) => OpenLastSavedPhoto();

    private void OpenLastSavedPhoto()
    {
        try
        {
            if (!string.IsNullOrEmpty(_lastSavedPhotoPath) && File.Exists(_lastSavedPhotoPath))
            {
                Process.Start(new ProcessStartInfo(_lastSavedPhotoPath) { UseShellExecute = true });
            }
            else
            {
                OpenPicturesFolder();
            }
        }
        catch (Exception ex)
        {
            Log.Warn("Failed to open saved photo", ex);
        }
    }

    private void BtnOpenPicturesFolder_Click(object sender, RoutedEventArgs e) => OpenPicturesFolder();

    private void OpenPicturesFolder()
    {
        try
        {
            string picturesDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "WindowsDynamicIsland");
            if (!Directory.Exists(picturesDir)) Directory.CreateDirectory(picturesDir);
            Process.Start(new ProcessStartInfo(picturesDir) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Log.Warn("Failed to open pictures folder", ex);
        }
    }

    private void BtnClosePrintedPhoto_Click(object sender, RoutedEventArgs e)
    {
        _retractPhotoTimer?.Stop();
        DismissPrintedPhoto();
    }

    private void PageDotSystem_Click(object sender, RoutedEventArgs e)
        => _stateMachine.SelectPage(ExpandedPage.System);

    private void PageDotClock_Click(object sender, RoutedEventArgs e)
        => _stateMachine.SelectPage(ExpandedPage.Clock);

    private void PageDotTools_Click(object sender, RoutedEventArgs e)
        => _stateMachine.SelectPage(ExpandedPage.Tools);

    private void PageDotWeather_Click(object sender, RoutedEventArgs e)
        => _stateMachine.SelectPage(ExpandedPage.Weather);

    private void BtnCollapse_Click(object sender, RoutedEventArgs e)
        => _stateMachine.SetExpanded(false);

    private async void BtnPlayPause_Click(object sender, RoutedEventArgs e)
        => await _mediaService.TogglePlayPauseAsync();

    private async void BtnPrevTrack_Click(object sender, RoutedEventArgs e)
        => await _mediaService.PreviousAsync();

    private async void BtnNextTrack_Click(object sender, RoutedEventArgs e)
        => await _mediaService.NextAsync();

    private void BtnQuickClipboard_Click(object sender, RoutedEventArgs e)
        => MonitorHelper.OpenClipboardHistory();

    private void BtnQuickTaskMgr_Click(object sender, RoutedEventArgs e)
    {
        try { Process.Start(new ProcessStartInfo("taskmgr.exe") { UseShellExecute = true }); }
        catch (Exception ex) { Log.Warn("Could not start Task Manager.", ex); }
    }

    private void BtnQuickSettings_Click(object sender, RoutedEventArgs e)
        => OpenSettings();

    // =========================================================================
    // TOOLS: STOPWATCH, TIMER & APP LAUNCHERS
    // =========================================================================

    private void BtnToggleTimerMode_Click(object sender, RoutedEventArgs e)
    {
        var newMode = _timerService.Mode == TimerMode.Stopwatch ? TimerMode.Countdown : TimerMode.Stopwatch;
        _timerService.SetMode(newMode);
        ToolsTimerModeText.Text = newMode == TimerMode.Stopwatch ? "STOPWATCH" : "5M TIMER";
        ToolsTimerModeText.Foreground = (TryFindResource(newMode == TimerMode.Stopwatch ? "Accent.Blue" : "Accent.Orange") as Brush) ?? Brushes.DodgerBlue;
    }

    private void BtnToolsTimerPlay_Click(object sender, RoutedEventArgs e)
        => _timerService.Toggle();

    private void BtnToolsTimerReset_Click(object sender, RoutedEventArgs e)
        => _timerService.Reset();

    private void OnTimerTick() => Dispatcher.Invoke(UpdateTimerDisplay);

    private void UpdateTimerDisplay()
    {
        var t = _timerService.Mode == TimerMode.Stopwatch ? _timerService.Elapsed : _timerService.CountdownRemaining;
        ToolsTimerDisplay.Text = $"{t.Minutes:D2}:{t.Seconds:D2}.{t.Milliseconds / 100}";
        ToolsTimerPlayIcon.Text = _timerService.IsRunning ? "\uE769" : "\uE768";
    }

    private void OnTimerCountdownCompleted()
    {
        Dispatcher.Invoke(() =>
        {
            ShowNotification(new NotificationInfo
            {
                Title = "Timer Finished!",
                Message = "Your 5-minute focus session is complete.",
                IconGlyph = "\uE823",
                Timeout = TimeSpan.FromSeconds(5)
            });
        });
    }

    private void BtnLaunchCalc_Click(object sender, RoutedEventArgs e)
    {
        try { Process.Start(new ProcessStartInfo("calc.exe") { UseShellExecute = true }); }
        catch (Exception ex) { Log.Warn("Could not launch calculator.", ex); }
    }

    private void BtnLaunchNotepad_Click(object sender, RoutedEventArgs e)
    {
        try { Process.Start(new ProcessStartInfo("notepad.exe") { UseShellExecute = true }); }
        catch (Exception ex) { Log.Warn("Could not launch notepad.", ex); }
    }

    private void BtnLaunchSnipping_Click(object sender, RoutedEventArgs e)
    {
        try { Process.Start(new ProcessStartInfo("snippingtool.exe") { UseShellExecute = true }); }
        catch (Exception ex) { Log.Warn("Could not launch snipping tool.", ex); }
    }

    // =========================================================================
    // WEATHER & REMINDERS
    // =========================================================================

    private void OnWeatherUpdated(WeatherInfo w) => Dispatcher.Invoke(() => UpdateWeatherDisplay(w));

    private void UpdateWeatherDisplay(WeatherInfo w)
    {
        WeatherTemp.Text = $"{w.Temperature}°";
        WeatherCondition.Text = w.Condition;
        WeatherIcon.Text = w.IconGlyph;
        WeatherLocation.Text = w.Location;
        WeatherDetails.Text = $"{w.HighLow} • {w.Humidity}";

        // Dashboard widgets (Image 2 style)
        DashLocation.Text = string.IsNullOrWhiteSpace(w.Location) ? "INDONESIA" : w.Location.ToUpperInvariant();
        DashTemperature.Text = $"{w.Temperature}°";
        DashWeatherIcon.Text = w.IconGlyph;
        DashCondition.Text = w.Condition;
        DashHighLow.Text = string.IsNullOrWhiteSpace(w.HighLow) ? "H 30° • L 22°" : w.HighLow;
    }

    private void OnRemindersChanged() => Dispatcher.Invoke(RenderReminders);

    private void RenderReminders()
    {
        ReminderListPanel.Children.Clear();
        var items = _reminderService.Items;
        ReminderCountText.Text = $"{items.Count} items";

        var iconFont = (TryFindResource("Font.Icons") as FontFamily) ?? new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets");
        var textFont = (TryFindResource("Font.Text") as FontFamily) ?? new FontFamily("Segoe UI");
        var greenBrush = (TryFindResource("Accent.Green") as Brush) ?? Brushes.LightGreen;
        var primaryBrush = (TryFindResource("Text.Primary") as Brush) ?? Brushes.White;
        var tertiaryBrush = (TryFindResource("Text.Tertiary") as Brush) ?? Brushes.Gray;

        foreach (var item in System.Linq.Enumerable.Take(items, 2))
        {
            var grid = new Grid { Margin = new Thickness(0, 2, 0, 2), Cursor = Cursors.Hand };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var check = new TextBlock
            {
                Text = item.IsCompleted ? "\uE73E" : "\uE739",
                FontFamily = iconFont,
                FontSize = 11,
                Foreground = item.IsCompleted ? greenBrush : tertiaryBrush,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 6, 0)
            };

            var text = new TextBlock
            {
                Text = item.Text,
                FontFamily = textFont,
                FontSize = 10.5,
                Foreground = item.IsCompleted ? tertiaryBrush : primaryBrush,
                TextDecorations = item.IsCompleted ? TextDecorations.Strikethrough : null,
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center
            };

            long id = item.Id;
            grid.MouseLeftButtonUp += (_, _) => _reminderService.Toggle(id);

            Grid.SetColumn(check, 0);
            Grid.SetColumn(text, 1);
            grid.Children.Add(check);
            grid.Children.Add(text);

            ReminderListPanel.Children.Add(grid);
        }
    }

    private void BtnAddReminder_Click(object sender, RoutedEventArgs e) => CommitNewReminder();

    private void ReminderInputBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            CommitNewReminder();
            e.Handled = true;
        }
    }

    private void CommitNewReminder()
    {
        string text = ReminderInputBox.Text.Trim();
        if (!string.IsNullOrEmpty(text) && text != "New reminder...")
        {
            _reminderService.Add(text);
            ReminderInputBox.Text = "";
        }
    }

    private void ReminderInputBox_GotFocus(object sender, RoutedEventArgs e)
    {
        if (ReminderInputBox.Text == "New reminder...")
            ReminderInputBox.Text = "";
    }

    private void ReminderInputBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(ReminderInputBox.Text))
            ReminderInputBox.Text = "New reminder...";
    }

    // =========================================================================
    // PUBLIC CONTROLS
    // =========================================================================

    public void ToggleVisibility()
    {
        if (Visibility == Visibility.Visible)
        {
            Visibility = Visibility.Collapsed;
        }
        else
        {
            Visibility = Visibility.Visible;
            UpdateWindowPlacement();
        }
    }

    public void OpenSettings()
    {
        if (_settingsWindow != null && _settingsWindow.IsLoaded)
        {
            _settingsWindow.Activate();
            return;
        }

        _settingsWindow = new SettingsWindow(_settingsService, _startupService);
        _settingsWindow.Owner = this;
        _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        _settingsWindow.Show();
    }

    // =========================================================================
    // DASHBOARD HARDWARE CONTROLS (Brightness & Volume)
    // =========================================================================

    private void RefreshDashboardHardwareControls()
    {
        Task.Run(() =>
        {
            int brightness = HardwareControlService.GetBrightness();
            int volume = HardwareControlService.GetVolume();
            bool isMuted = HardwareControlService.GetMute();

            Dispatcher.InvokeAsync(() =>
            {
                _isUpdatingHardwareSliders = true;
                try
                {
                    if (SliderBrightness != null)
                    {
                        SliderBrightness.Value = brightness;
                    }
                    if (DashBrightnessVal != null)
                    {
                        DashBrightnessVal.Text = $"{brightness}%";
                    }
                    if (SliderVolume != null)
                    {
                        SliderVolume.Value = isMuted ? 0 : volume;
                    }
                    if (DashVolumeVal != null)
                    {
                        DashVolumeVal.Text = isMuted ? "Muted" : $"{volume}%";
                    }
                    UpdateVolumeMuteUI(isMuted);
                }
                finally
                {
                    _isUpdatingHardwareSliders = false;
                }
            });
        });
    }

    private void SliderBrightness_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isUpdatingHardwareSliders) return;
        int val = (int)Math.Round(e.NewValue);
        if (DashBrightnessVal != null)
        {
            DashBrightnessVal.Text = $"{val}%";
        }
        HardwareControlService.SetBrightness(val);
    }

    private void SliderVolume_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isUpdatingHardwareSliders) return;
        int val = (int)Math.Round(e.NewValue);
        if (DashVolumeVal != null)
        {
            DashVolumeVal.Text = $"{val}%";
        }
        HardwareControlService.SetVolume(val);
        if (val > 0)
        {
            HardwareControlService.SetMute(false);
            UpdateVolumeMuteUI(false);
        }
    }

    private void BtnDashBrightness_Click(object sender, RoutedEventArgs e)
    {
        if (SliderBrightness == null) return;
        int cur = (int)Math.Round(SliderBrightness.Value);
        int target = cur >= 80 ? 30 : 100;
        SliderBrightness.Value = target;
    }

    private void BtnDashVolumeMute_Click(object sender, RoutedEventArgs e)
    {
        bool isMuted = HardwareControlService.GetMute();
        bool newMute = !isMuted;
        HardwareControlService.SetMute(newMute);
        UpdateVolumeMuteUI(newMute);
        if (newMute && DashVolumeVal != null)
        {
            DashVolumeVal.Text = "Muted";
        }
        else if (DashVolumeVal != null && SliderVolume != null)
        {
            DashVolumeVal.Text = $"{(int)Math.Round(SliderVolume.Value)}%";
        }
    }

    private void UpdateVolumeMuteUI(bool isMuted)
    {
        if (DashVolumeIcon != null)
        {
            DashVolumeIcon.Text = isMuted ? "\uE74F" : "\uE767";
            DashVolumeIcon.Foreground = isMuted
                ? (Brush?)TryFindResource("Accent.Red") ?? Brushes.Red
                : (Brush?)TryFindResource("Accent.Blue") ?? Brushes.DodgerBlue;
        }
        if (DashMuteIcon != null)
        {
            DashMuteIcon.Foreground = isMuted
                ? (Brush?)TryFindResource("Accent.Red") ?? Brushes.Red
                : (Brush?)TryFindResource("Text.Secondary") ?? Brushes.Gray;
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        if (_isClosing) return;
        _isClosing = true;

        _clockTimer.Stop();
        _timelineTimer.Stop();
        _hoverTimer.Stop();
        _autoCollapseTimer.Stop();
        _notificationTimer.Stop();

        base.OnClosed(e);
    }
}