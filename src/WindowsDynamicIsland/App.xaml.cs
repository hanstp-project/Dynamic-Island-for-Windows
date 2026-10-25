using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using WindowsDynamicIsland.Animations;
using WindowsDynamicIsland.Infrastructure;
using WindowsDynamicIsland.Models;
using WindowsDynamicIsland.Services;
using WindowsDynamicIsland.Themes;
using Forms = System.Windows.Forms;

namespace WindowsDynamicIsland;

public partial class App : Application
{
    private SingleInstanceManager? _singleInstance;
    private LoggingService? _loggingService;
    private SettingsService? _settingsService;
    private StartupService? _startupService;
    private MediaSessionService? _mediaService;
    private SystemMonitorService? _systemService;
    private IslandStateMachine? _stateMachine;
    private readonly List<MainWindow> _islandWindows = new();
    private ReminderService? _reminderService;
    private WindowsNotificationService? _notificationService;
    private Forms.NotifyIcon? _trayIcon;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WindowsDynamicIsland");
        _loggingService = new LoggingService(Path.Combine(appData, "Logs"));
        Log.Initialize(_loggingService);
        Log.Info("=== Application Startup Initiated ===");

        DispatcherUnhandledException += (s, args) =>
        {
            Log.Error("Unhandled Dispatcher Exception: " + args.Exception?.Message, args.Exception);
            args.Handled = true;
        };

        AppDomain.CurrentDomain.UnhandledException += (s, args) =>
        {
            var ex = args.ExceptionObject as Exception;
            Log.Error("Unhandled Domain Exception: " + ex?.Message, ex);
        };

        try
        {
            // Command line: --shutdown
            if (e.Args.Length > 0 && e.Args[0].Equals("--shutdown", StringComparison.OrdinalIgnoreCase))
            {
                Log.Info("Received --shutdown argument. Requesting instance shutdown.");
                SingleInstanceManager.RequestShutdownOfRunningInstance(TimeSpan.FromSeconds(3));
                Shutdown();
                return;
            }

            // Single instance check
            _singleInstance = new SingleInstanceManager();
            Log.Info($"SingleInstance: IsPrimary={_singleInstance.IsPrimary}");
            if (!_singleInstance.IsPrimary)
            {
                Log.Info("Another instance is already running. Signalling it and shutting down.");
                _singleInstance.SignalActivate();
                Shutdown();
                return;
            }

            // Load Settings
            _settingsService = new SettingsService(appData);
            var settings = _settingsService.Load();
            Log.Info($"Settings loaded. Theme={settings.Theme}, MonitorIndex={settings.MonitorIndex}, ShowOnAllMonitors={settings.ShowOnAllMonitors}");

            // Apply Theme
            ThemeManager.Apply(Resources, settings.Theme);

            // Startup Registration
            _startupService = new StartupService();
            _startupService.RepairIfStale();
            if (settings.StartWithWindows != _startupService.IsEnabled())
            {
                settings.StartWithWindows = _startupService.IsEnabled();
            }

            // Initialize Services
            _mediaService = new MediaSessionService();
            _systemService = new SystemMonitorService(Dispatcher);
            _stateMachine = new IslandStateMachine();
            _reminderService = new ReminderService(_settingsService.Directory);
            _notificationService = new WindowsNotificationService(_settingsService);
            _notificationService.NotificationReceived += BroadcastNotification;
            _ = _notificationService.StartAsync();

            _systemService.Start();
            Log.Info("SystemService & NotificationService started.");

            // Listen for second-instance signals
            _singleInstance.Listen(
                onActivate: () => Dispatcher.Invoke(() =>
                {
                    Log.Info("Received secondary instance activation signal. Expanding all islands.");
                    foreach (var win in _islandWindows)
                    {
                        win.Visibility = Visibility.Visible;
                        win.UpdateWindowPlacement();
                        win.Topmost = false;
                        win.Topmost = true;
                        win.Activate();
                        win.StateMachine.SetExpanded(true);
                    }
                }),
                onShutdown: () => Dispatcher.Invoke(() =>
                {
                    Log.Info("Received shutdown signal from secondary instance.");
                    Shutdown();
                }));

            // Create Main Floating Island Windows for connected screens
            Log.Info("Setting up island windows for monitors...");
            SetupWindows();

            // Setup System Tray Icon on STA thread
            Log.Info("Initializing System Tray Icon...");
            InitializeTrayIcon();

            // Listen for display changes (monitors plugged / unplugged)
            Microsoft.Win32.SystemEvents.DisplaySettingsChanged += (_, _) =>
            {
                Dispatcher.Invoke(() =>
                {
                    Log.Info("DisplaySettingsChanged detected. Refreshing island placement...");
                    SetupWindows();
                });
            };

            // Listen for settings change
            _settingsService.Changed += prop =>
            {
                if (prop == null || prop == nameof(AppSettings.ShowOnAllMonitors) || prop == nameof(AppSettings.MonitorIndex))
                {
                    Dispatcher.Invoke(() =>
                    {
                        Log.Info("Monitor configuration changed. Rebuilding windows...");
                        SetupWindows();
                    });
                }
            };

            // Start media service asynchronously without delaying UI startup
            _ = Task.Run(async () =>
            {
                try
                {
                    await _mediaService.StartAsync();
                    Log.Info("MediaService started.");
                }
                catch (Exception ex)
                {
                    Log.Warn("MediaService failed to start (ignoring).", ex);
                }
            });

            Log.Info("=== Windows Dynamic Island started successfully and is running. ===");
        }
        catch (Exception ex)
        {
            Log.Error("Fatal startup failure in OnStartup!", ex);
            MessageBox.Show($"Startup Error: {ex.Message}\n\nCheck logs at: {Path.Combine(appData, "Logs", "app.log")}",
                "Windows Dynamic Island Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
        }
    }

    private void SetupWindows()
    {
        var settings = _settingsService?.Current;
        if (settings == null) return;

        foreach (var win in _islandWindows.ToList())
        {
            try { win.Close(); } catch { }
        }
        _islandWindows.Clear();

        var monitors = Interop.MonitorHelper.GetMonitors();
        Log.Info($"SetupWindows: Detected {monitors.Count} monitor(s). ShowOnAllMonitors={settings.ShowOnAllMonitors}");

        if (settings.ShowOnAllMonitors && monitors.Count > 1)
        {
            for (int i = 1; i <= monitors.Count; i++)
            {
                var sm = (i == 1) ? _stateMachine! : new IslandStateMachine();
                var win = new MainWindow(_settingsService!, sm, _mediaService!, _systemService!, _startupService!, _reminderService, i);
                _islandWindows.Add(win);
            }
        }
        else
        {
            var win = new MainWindow(_settingsService!, _stateMachine!, _mediaService!, _systemService!, _startupService!, _reminderService, settings.MonitorIndex);
            _islandWindows.Add(win);
        }

        MainWindow = _islandWindows.FirstOrDefault();

        foreach (var win in _islandWindows)
        {
            if (settings.ShowPillOnStartup)
            {
                win.Show();
                win.Topmost = true;
                win.Activate();
                win.StateMachine.SetExpanded(true);
            }
        }
    }

    private void InitializeTrayIcon()
    {
        _trayIcon = new Forms.NotifyIcon
        {
            Text = "Windows Dynamic Island",
            Visible = true,
        };

        try
        {
            var iconUri = new Uri("pack://application:,,,/Resources/Icons/app.ico");
            var stream = GetResourceStream(iconUri)?.Stream;
            if (stream != null)
            {
                _trayIcon.Icon = new Icon(stream);
            }
            else
            {
                _trayIcon.Icon = SystemIcons.Application;
            }
        }
        catch (Exception ex)
        {
            Log.Warn("Could not load icon for notify icon.", ex);
            _trayIcon.Icon = SystemIcons.Application;
        }

        var contextMenu = new Forms.ContextMenuStrip();

        contextMenu.Items.Add("Toggle Dynamic Island", null, (_, _) =>
        {
            Dispatcher.Invoke(() =>
            {
                foreach (var win in _islandWindows) win.ToggleVisibility();
            });
        });

        contextMenu.Items.Add("Play / Pause Media", null, async (_, _) =>
        {
            if (_mediaService != null) await _mediaService.TogglePlayPauseAsync();
        });

        contextMenu.Items.Add("Next Track", null, async (_, _) =>
        {
            if (_mediaService != null) await _mediaService.NextAsync();
        });

        contextMenu.Items.Add("Switch Style (Dynamic Island / Notch)", null, (_, _) =>
        {
            Dispatcher.Invoke(() =>
            {
                var s = _settingsService?.Current;
                if (s != null)
                {
                    s.Style = s.Style == IslandStyle.DynamicIsland ? IslandStyle.Notch : IslandStyle.DynamicIsland;
                    _settingsService?.NotifyChanged(nameof(AppSettings.Style));
                }
            });
        });

        contextMenu.Items.Add(new Forms.ToolStripSeparator());

        contextMenu.Items.Add("Settings...", null, (_, _) =>
        {
            Dispatcher.Invoke(() => _islandWindows.FirstOrDefault()?.OpenSettings());
        });

        contextMenu.Items.Add(new Forms.ToolStripSeparator());

        contextMenu.Items.Add("Exit", null, (_, _) =>
        {
            Dispatcher.Invoke(Shutdown);
        });

        _trayIcon.ContextMenuStrip = contextMenu;
        _trayIcon.DoubleClick += (_, _) =>
        {
            Dispatcher.Invoke(() =>
            {
                foreach (var win in _islandWindows) win.ToggleVisibility();
            });
        };
    }

    public void BroadcastNotification(NotificationInfo info)
    {
        var s = _settingsService?.Current;
        if (s != null && !s.NotificationsEnabled) return;

        Dispatcher.InvokeAsync(() =>
        {
            Log.Info($"Broadcasting Notification: [{info.AppName}] {info.Title} - {info.Message}");
            foreach (var win in _islandWindows)
            {
                win.ShowNotification(info);
            }
        });
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Log.Info("Shutting down Windows Dynamic Island...");

        foreach (var win in _islandWindows)
        {
            try { win.Close(); } catch { }
        }
        _islandWindows.Clear();

        if (_trayIcon != null)
        {
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
            _trayIcon = null;
        }

        _notificationService?.Stop();
        _notificationService?.Dispose();
        _mediaService?.Stop();
        _mediaService?.Dispose();
        _systemService?.Stop();
        _systemService?.Dispose();
        _settingsService?.Dispose();
        _singleInstance?.Dispose();

        Log.Shutdown();

        base.OnExit(e);
    }
}
