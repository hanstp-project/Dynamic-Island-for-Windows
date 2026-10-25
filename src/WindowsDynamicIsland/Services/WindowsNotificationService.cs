using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Windows.UI.Notifications;
using Windows.UI.Notifications.Management;
using WindowsDynamicIsland.Models;

namespace WindowsDynamicIsland.Services;

/// <summary>
/// Service listening for Windows Action Center toast notifications (WhatsApp, Telegram, Web browsers,
/// Outlook/Email, Discord, System) via UserNotificationListener and delivering them as authentic
/// macOS / iOS Dynamic Island notification banners.
/// </summary>
public sealed class WindowsNotificationService : IDisposable
{
    private readonly SettingsService _settingsService;
    private UserNotificationListener? _listener;
    private bool _isListening;
    private bool _disposed;
    private readonly HashSet<uint> _seenNotificationIds = new();
    private CancellationTokenSource? _pollingCts;

    public event Action<NotificationInfo>? NotificationReceived;

    private HttpListener? _httpListener;

    public WindowsNotificationService(SettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    public async Task StartAsync()
    {
        if (_disposed) return;

        StartLocalHttpServer();

        if (_isListening) return;

        try
        {
            if (UserNotificationListener.Current != null)
            {
                var access = await UserNotificationListener.Current.RequestAccessAsync();
                Log.Info($"UserNotificationListener access status: {access}");
                if (access == UserNotificationListenerAccessStatus.Allowed)
                {
                    _listener = UserNotificationListener.Current;
                    _isListening = true;

                    // 1. Seed seen IDs from current active notifications so we don't pop up old notifications on initial launch
                    try
                    {
                        var currentNotifs = await _listener.GetNotificationsAsync(NotificationKinds.Toast);
                        if (currentNotifs != null)
                        {
                            foreach (var n in currentNotifs)
                            {
                                _seenNotificationIds.Add(n.Id);
                            }
                            Log.Info($"Seeded {_seenNotificationIds.Count} existing active toast notification(s).");
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Warn("Failed to seed initial notifications: " + ex.Message);
                    }

                    // 2. Try registering for NotificationChanged event (supported when packaged or on specific Win11 builds)
                    try
                    {
                        _listener.NotificationChanged += OnNotificationChanged;
                        Log.Info("UserNotificationListener successfully registered for NotificationChanged.");
                    }
                    catch (Exception ex)
                    {
                        Log.Info("UserNotificationListener NotificationChanged event subscription not supported in unpackaged context (using polling fallback): " + ex.Message);
                    }

                    // 3. Start high-frequency background polling loop (essential for unpackaged Win32 apps)
                    _pollingCts?.Cancel();
                    _pollingCts = new CancellationTokenSource();
                    _ = Task.Run(() => PollNotificationsLoopAsync(_pollingCts.Token));
                }
                else
                {
                    Log.Warn($"UserNotificationListener access not allowed (Status: {access}). Notifications cannot be captured.");
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error("UserNotificationListener initialization error: " + ex.Message, ex);
        }
    }

    private async Task PollNotificationsLoopAsync(CancellationToken token)
    {
        Log.Info("Notification polling loop started (interval: 500ms).");
        while (!token.IsCancellationRequested && !_disposed)
        {
            try
            {
                await Task.Delay(500, token);

                if (_disposed || _listener == null) break;

                var s = _settingsService.Current;
                if (!s.NotificationsEnabled || !s.MirrorWindowsNotifications)
                {
                    continue;
                }

                IReadOnlyList<UserNotification>? currentNotifs;
                try
                {
                    currentNotifs = await _listener.GetNotificationsAsync(NotificationKinds.Toast);
                }
                catch (Exception ex)
                {
                    Log.Debug("GetNotificationsAsync query error: " + ex.Message);
                    await Task.Delay(500, token);
                    continue;
                }

                if (currentNotifs == null || currentNotifs.Count == 0) continue;

                var activeIds = new HashSet<uint>();

                foreach (var notif in currentNotifs)
                {
                    activeIds.Add(notif.Id);

                    // If it's a new notification ID that hasn't been seen yet:
                    if (_seenNotificationIds.Add(notif.Id))
                    {
                        Log.Info($"New toast notification detected! ID={notif.Id}");
                        ProcessNotification(notif);
                    }
                }

                // Periodic cleanup: keep _seenNotificationIds bounded, preserving IDs currently active in Action Center
                if (_seenNotificationIds.Count > 1000)
                {
                    _seenNotificationIds.RemoveWhere(id => !activeIds.Contains(id));
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Log.Debug("Notification polling loop exception: " + ex.Message);
            }
        }
    }

    private void StartLocalHttpServer()
    {
        try
        {
            if (_httpListener != null) return;
            _httpListener = new HttpListener();
            _httpListener.Prefixes.Add("http://127.0.0.1:23851/notify/");
            _httpListener.Start();
            _ = Task.Run(ListenHttpLoopAsync);
            Log.Info("Notification HTTP webhook active at http://127.0.0.1:23851/notify/");
        }
        catch (Exception ex)
        {
            Log.Warn("Could not start notification HTTP listener: " + ex.Message);
        }
    }

    private async Task ListenHttpLoopAsync()
    {
        while (_httpListener != null && _httpListener.IsListening && !_disposed)
        {
            try
            {
                var ctx = await _httpListener.GetContextAsync();
                _ = Task.Run(() => HandleHttpRequest(ctx));
            }
            catch when (_disposed)
            {
                break;
            }
            catch { }
        }
    }

    private void HandleHttpRequest(HttpListenerContext ctx)
    {
        try
        {
            var req = ctx.Request;
            var res = ctx.Response;

            // Allow CORS from browser web pages (WhatsApp Web, Gmail, etc.)
            res.Headers.Add("Access-Control-Allow-Origin", "*");
            res.Headers.Add("Access-Control-Allow-Methods", "POST, GET, OPTIONS");
            res.Headers.Add("Access-Control-Allow-Headers", "Content-Type");

            if (req.HttpMethod.Equals("OPTIONS", StringComparison.OrdinalIgnoreCase))
            {
                res.StatusCode = 200;
                res.Close();
                return;
            }

            string app = req.QueryString["app"] ?? "";
            string title = req.QueryString["title"] ?? "";
            string message = req.QueryString["message"] ?? req.QueryString["msg"] ?? "";
            int duration = _settingsService.Current.NotificationTimeoutSeconds;

            if (int.TryParse(req.QueryString["duration"], out int d) && d > 0)
            {
                duration = d;
            }

            if (req.HttpMethod.Equals("POST", StringComparison.OrdinalIgnoreCase) && req.HasEntityBody)
            {
                using var reader = new StreamReader(req.InputStream, req.ContentEncoding ?? Encoding.UTF8);
                string body = reader.ReadToEnd();
                if (!string.IsNullOrWhiteSpace(body))
                {
                    try
                    {
                        using var doc = JsonDocument.Parse(body);
                        var root = doc.RootElement;
                        if (root.TryGetProperty("app", out var aProp) || root.TryGetProperty("appName", out aProp))
                            app = aProp.GetString() ?? app;
                        if (root.TryGetProperty("title", out var tProp))
                            title = tProp.GetString() ?? title;
                        if (root.TryGetProperty("message", out var mProp) || root.TryGetProperty("body", out mProp))
                            message = mProp.GetString() ?? message;
                        if ((root.TryGetProperty("duration", out var dProp) || root.TryGetProperty("timeout", out dProp)) && dProp.TryGetInt32(out int bodyDur) && bodyDur > 0)
                            duration = bodyDur;
                    }
                    catch
                    {
                        if (string.IsNullOrWhiteSpace(message)) message = body;
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(title) || !string.IsNullOrWhiteSpace(message))
            {
                var (resolvedApp, glyph, colorHex) = ResolveAppDetails(app, "", title, message);
                var info = new NotificationInfo
                {
                    Kind = NotificationKind.Windows,
                    AppName = resolvedApp,
                    Title = string.IsNullOrWhiteSpace(title) ? resolvedApp : title,
                    Message = message,
                    IconGlyph = glyph,
                    AccentColor = colorHex,
                    Timeout = TimeSpan.FromSeconds(Math.Max(1, duration)),
                    CreatedAt = DateTimeOffset.Now
                };

                NotificationReceived?.Invoke(info);

                byte[] respBytes = Encoding.UTF8.GetBytes("{\"ok\":true}");
                res.ContentType = "application/json";
                res.StatusCode = 200;
                res.OutputStream.Write(respBytes, 0, respBytes.Length);
            }
            else
            {
                byte[] respBytes = Encoding.UTF8.GetBytes("{\"ok\":false,\"error\":\"title or message required\"}");
                res.ContentType = "application/json";
                res.StatusCode = 400;
                res.OutputStream.Write(respBytes, 0, respBytes.Length);
            }

            res.Close();
        }
        catch { }
    }

    public void Stop()
    {
        _pollingCts?.Cancel();
        _pollingCts = null;

        if (_listener != null && _isListening)
        {
            try
            {
                _listener.NotificationChanged -= OnNotificationChanged;
            }
            catch { }
            _isListening = false;
        }

        try
        {
            _httpListener?.Stop();
            _httpListener?.Close();
            _httpListener = null;
        }
        catch { }
    }

    private void OnNotificationChanged(UserNotificationListener sender, UserNotificationChangedEventArgs args)
    {
        if (_disposed) return;
        var s = _settingsService.Current;
        if (!s.NotificationsEnabled || !s.MirrorWindowsNotifications) return;

        if (args.ChangeKind == UserNotificationChangedKind.Added)
        {
            try
            {
                if (!_seenNotificationIds.Add(args.UserNotificationId))
                {
                    return;
                }

                var notif = sender.GetNotification(args.UserNotificationId);
                if (notif != null)
                {
                    ProcessNotification(notif);
                }
            }
            catch (Exception ex)
            {
                Log.Warn("Failed to process incoming UserNotification: " + ex.Message);
            }
        }
    }

    public void ProcessNotification(UserNotification notif)
    {
        try
        {
            string rawAppName = notif.AppInfo?.DisplayInfo?.DisplayName ?? "";
            string appId = notif.AppInfo?.Id ?? "";

            string title = "";
            string message = "";

            var visual = notif.Notification?.Visual;
            NotificationBinding? binding = null;
            try
            {
                binding = visual?.GetBinding(KnownNotificationBindings.ToastGeneric);
                if (binding == null && visual?.Bindings != null && visual.Bindings.Count > 0)
                {
                    binding = visual.Bindings[0];
                }
            }
            catch { }

            if (binding != null)
            {
                try
                {
                    var textElements = binding.GetTextElements();
                    if (textElements != null)
                    {
                        var texts = textElements
                            .Select(t => t?.Text?.Trim())
                            .Where(t => !string.IsNullOrEmpty(t))
                            .ToList();

                        if (texts.Count == 1)
                        {
                            title = texts[0]!;
                        }
                        else if (texts.Count == 2)
                        {
                            title = texts[0]!;
                            message = texts[1]!;
                        }
                        else if (texts.Count >= 3)
                        {
                            // If first line matches app name (e.g. WhatsApp, Telegram), use line 1 as sender/title and line 2+ as body
                            if (texts[0]!.Equals(rawAppName, StringComparison.OrdinalIgnoreCase) ||
                                texts[0]!.Contains("whatsapp", StringComparison.OrdinalIgnoreCase) ||
                                texts[0]!.Contains("telegram", StringComparison.OrdinalIgnoreCase))
                            {
                                title = texts[1]!;
                                message = string.Join(" • ", texts.Skip(2));
                            }
                            else
                            {
                                title = texts[0]!;
                                message = string.Join(" • ", texts.Skip(1));
                            }
                        }
                    }
                }
                catch { }
            }

            if (string.IsNullOrWhiteSpace(title) && string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            var (appName, glyph, colorHex) = ResolveAppDetails(rawAppName, appId, title, message);

            if (string.IsNullOrWhiteSpace(title))
            {
                title = appName;
            }

            if (string.IsNullOrWhiteSpace(message) && title.Contains(':'))
            {
                var parts = title.Split(new[] { ':' }, 2);
                title = parts[0].Trim();
                message = parts[1].Trim();
            }

            var info = new NotificationInfo
            {
                Kind = NotificationKind.Windows,
                AppName = appName,
                Title = title,
                Message = message,
                IconGlyph = glyph,
                AccentColor = colorHex,
                Timeout = TimeSpan.FromSeconds(_settingsService.Current.NotificationTimeoutSeconds),
                CreatedAt = DateTimeOffset.Now
            };

            Log.Info($"[WindowsNotification] Captured: [{appName}] '{title}' - '{message}'");
            NotificationReceived?.Invoke(info);
        }
        catch (Exception ex)
        {
            Log.Warn("Failed to process notification: " + ex.Message);
        }
    }

    public static (string AppName, string Glyph, string ColorHex) ResolveAppDetails(
        string rawAppName, string appId, string title, string message)
    {
        string combined = $"{rawAppName} {appId} {title}".ToLowerInvariant();

        // 1. WhatsApp (Desktop or Web)
        if (combined.Contains("whatsapp") || rawAppName.Contains("whatsapp", StringComparison.OrdinalIgnoreCase))
        {
            return ("WhatsApp", "💬", "#25D366");
        }

        // 2. Telegram
        if (combined.Contains("telegram") || rawAppName.Contains("telegram", StringComparison.OrdinalIgnoreCase))
        {
            return ("Telegram", "✈️", "#2AABEE");
        }

        // 3. Discord
        if (combined.Contains("discord"))
        {
            return ("Discord", "🎮", "#5865F2");
        }

        // 4. Microsoft Teams
        if (combined.Contains("teams"))
        {
            return ("Microsoft Teams", "👥", "#6264A7");
        }

        // 5. Slack
        if (combined.Contains("slack"))
        {
            return ("Slack", "💼", "#E01E5A");
        }

        // 6. Signal
        if (combined.Contains("signal"))
        {
            return ("Signal", "💬", "#3A76F0");
        }

        // 7. LINE
        if (combined.Contains("line"))
        {
            return ("LINE", "🟢", "#06C755");
        }

        // 8. Messenger / Facebook
        if (combined.Contains("messenger") || combined.Contains("facebook"))
        {
            return ("Messenger", "💬", "#0084FF");
        }

        // 9. Instagram
        if (combined.Contains("instagram"))
        {
            return ("Instagram", "📷", "#E1306C");
        }

        // 10. Email / Outlook / Thunderbird
        if (combined.Contains("outlook") || combined.Contains("thunderbird") || rawAppName.Contains("mail", StringComparison.OrdinalIgnoreCase))
        {
            return (string.IsNullOrWhiteSpace(rawAppName) ? "Mail" : rawAppName, "✉️", "#0078D4");
        }

        // 11. Spotify
        if (combined.Contains("spotify"))
        {
            return ("Spotify", "🎵", "#1DB954");
        }

        // 12. Browsers
        if (combined.Contains("chrome")) return ("Google Chrome", "🌐", "#EA4335");
        if (combined.Contains("edge")) return ("Microsoft Edge", "🌐", "#0078D7");
        if (combined.Contains("firefox")) return ("Firefox", "🦊", "#FF7139");
        if (combined.Contains("opera") || combined.Contains("brave")) return ("Browser", "🌐", "#FF1B2D");

        // 13. System / Windows Defender
        if (combined.Contains("windows") || combined.Contains("defender") || combined.Contains("security") || combined.Contains("system"))
        {
            return (string.IsNullOrWhiteSpace(rawAppName) ? "Windows" : rawAppName, "💻", "#0078D4");
        }

        // Fallback: determine readable name
        string resolvedName = !string.IsNullOrWhiteSpace(rawAppName) && !rawAppName.Equals("App", StringComparison.OrdinalIgnoreCase)
            ? rawAppName
            : (!string.IsNullOrWhiteSpace(appId) ? appId : "Notification");

        return (resolvedName, "🔔", "#3B82F6");
    }

    public static (string Glyph, string ColorHex) GetAppBadge(string appName)
    {
        var (_, glyph, colorHex) = ResolveAppDetails(appName, "", "", "");
        return (glyph, colorHex);
    }

    public void DispatchCustomNotification(string title, string message, string appName, string glyph = "🔔", string colorHex = "#3B82F6", int? durationSeconds = null)
    {
        int sec = durationSeconds ?? _settingsService.Current.NotificationTimeoutSeconds;
        var info = new NotificationInfo
        {
            Kind = NotificationKind.System,
            AppName = appName,
            Title = title,
            Message = message,
            IconGlyph = glyph,
            AccentColor = colorHex,
            Timeout = TimeSpan.FromSeconds(Math.Max(1, sec)),
            CreatedAt = DateTimeOffset.Now
        };
        NotificationReceived?.Invoke(info);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
    }
}

