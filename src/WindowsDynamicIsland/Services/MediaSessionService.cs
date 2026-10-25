using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using Windows.Media.Control;
using Windows.Storage.Streams;
using WindowsDynamicIsland.Models;

namespace WindowsDynamicIsland.Services;

/// <summary>
/// Integrates with Windows Global System Media Transport Controls (GSMTC) – the same API the
/// Windows volume flyout uses. Works with any app that publishes a media session
/// (Spotify, browsers, Media Player, etc.).
///
/// Design notes:
/// - Fully event-driven (no polling). WinRT events arrive on thread-pool threads;
///   <see cref="MediaChanged"/> is therefore raised on a background thread.
/// - Bursts of events are coalesced (debounced) into a single refresh.
/// - Artwork is decoded at a small size, frozen, and kept in a tiny LRU cache.
/// - Command results are reported truthfully (the bool returned by Windows).
/// </summary>
public sealed class MediaSessionService : IDisposable
{
    private const int ArtworkDecodeSize = 160;
    private const int ArtworkCacheSize = 8;
    private const long MaxArtworkBytes = 8 * 1024 * 1024;

    private readonly SemaphoreSlim _refreshGate = new(1, 1);
    private readonly object _sync = new();
    private readonly LinkedList<(string Key, BitmapSource? Image)> _artCache = new();

    private GlobalSystemMediaTransportControlsSessionManager? _manager;
    private GlobalSystemMediaTransportControlsSession? _session;
    private CancellationTokenSource? _debounce;
    private bool _disposed;

    public MediaInfo Current { get; private set; } = MediaInfo.None;
    public bool IsAvailable => _manager is not null;
    public string? UnavailableReason { get; private set; }

    /// <summary>Raised on a background thread whenever the visible media state changes.</summary>
    public event Action<MediaInfo>? MediaChanged;

    public async Task StartAsync()
    {
        if (_manager is not null || _disposed) return;
        try
        {
            _manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
        }
        catch (Exception ex)
        {
            UnavailableReason = "Windows media session API is not available on this system.";
            Log.Warn("GSMTC manager could not be created.", ex);
            return;
        }

        _manager.CurrentSessionChanged += OnCurrentSessionChanged;
        _manager.SessionsChanged += OnSessionsChanged;
        Log.Info("Media session integration started.");
        AttachBestSession();
    }

    public void Stop()
    {
        lock (_sync)
        {
            _debounce?.Cancel();
            _debounce?.Dispose();
            _debounce = null;
            DetachSession();
            if (_manager is not null)
            {
                _manager.CurrentSessionChanged -= OnCurrentSessionChanged;
                _manager.SessionsChanged -= OnSessionsChanged;
                _manager = null;
            }
        }
        Publish(MediaInfo.None);
        Log.Info("Media session integration stopped.");
    }

    // ------------------------------------------------------------------ Commands

    public Task<bool> TogglePlayPauseAsync() => RunCommand(async s =>
    {
        var playback = s.GetPlaybackInfo();
        var controls = playback.Controls;
        if (controls.IsPlayPauseToggleEnabled) return await s.TryTogglePlayPauseAsync();
        bool playing = playback.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
        if (playing && controls.IsPauseEnabled) return await s.TryPauseAsync();
        if (!playing && controls.IsPlayEnabled) return await s.TryPlayAsync();
        return false;
    }, "play/pause");

    public Task<bool> NextAsync() => RunCommand(async s => await s.TrySkipNextAsync(), "next");
    public Task<bool> PreviousAsync() => RunCommand(async s => await s.TrySkipPreviousAsync(), "previous");

    private DateTimeOffset _seekSuppressionUntil = DateTimeOffset.MinValue;

    public Task<bool> ChangePlaybackPositionAsync(TimeSpan position) => RunCommand(async s =>
    {
        var tl = s.GetTimelineProperties();
        long startTicks = tl.StartTime > TimeSpan.Zero ? tl.StartTime.Ticks : 0;
        long targetTicks = startTicks + position.Ticks;

        long minTicks = tl.MinSeekTime > TimeSpan.Zero ? tl.MinSeekTime.Ticks : 0;
        long maxTicks = tl.MaxSeekTime > TimeSpan.Zero ? tl.MaxSeekTime.Ticks : (Current.Duration.Ticks > 0 ? Current.Duration.Ticks : TimeSpan.FromHours(10).Ticks);
        if (maxTicks > minTicks)
        {
            targetTicks = Math.Clamp(targetTicks, minTicks, maxTicks);
        }

        bool ok = await s.TryChangePlaybackPositionAsync(targetTicks);
        Log.Info($"Seek to {position} (ticks={targetTicks}) result: {ok}");

        _seekSuppressionUntil = DateTimeOffset.Now.AddMilliseconds(1500);
        var updated = Current with { Position = position, PositionTimestamp = DateTimeOffset.Now };
        Publish(updated);
        return ok;
    }, "seek");

    public Task<bool> SeekRelativeAsync(TimeSpan delta)
    {
        var currentPos = Current.EstimatePosition(DateTimeOffset.Now);
        return ChangePlaybackPositionAsync(currentPos + delta);
    }

    private async Task<bool> RunCommand(Func<GlobalSystemMediaTransportControlsSession, Task<bool>> command, string name)
    {
        var session = _session;
        if (session is null) return false;
        try
        {
            bool ok = await command(session).ConfigureAwait(false);
            if (!ok) Log.Info($"Media command '{name}' was rejected by the source application.");
            int delayMs = (name == "seek") ? 2500 : 150;
            ScheduleRefresh(delayMs);
            return ok;
        }
        catch (Exception ex)
        {
            Log.Warn($"Media command '{name}' failed (session may have closed).", ex);
            ScheduleRefresh(0);
            return false;
        }
    }

    // ------------------------------------------------------------------ Session tracking

    private void OnCurrentSessionChanged(GlobalSystemMediaTransportControlsSessionManager sender, CurrentSessionChangedEventArgs args)
        => AttachBestSession();

    private void OnSessionsChanged(GlobalSystemMediaTransportControlsSessionManager sender, SessionsChangedEventArgs args)
        => AttachBestSession();

    private void AttachBestSession()
    {
        lock (_sync)
        {
            var manager = _manager;
            if (manager is null || _disposed) return;

            GlobalSystemMediaTransportControlsSession? best = null;
            try
            {
                best = manager.GetCurrentSession();
                if (best is null)
                {
                    var sessions = manager.GetSessions();
                    best = sessions.FirstOrDefault(IsPlaying) ?? sessions.FirstOrDefault();
                }
            }
            catch (Exception ex)
            {
                Log.Warn("Failed to enumerate media sessions.", ex);
            }

            if (!ReferenceEquals(best, _session))
            {
                DetachSession();
                _session = best;
                if (best is not null)
                {
                    best.MediaPropertiesChanged += OnMediaPropertiesChanged;
                    best.PlaybackInfoChanged += OnPlaybackInfoChanged;
                    best.TimelinePropertiesChanged += OnTimelineChanged;
                }
            }
        }
        ScheduleRefresh(0);
    }

    private static bool IsPlaying(GlobalSystemMediaTransportControlsSession s)
    {
        try { return s.GetPlaybackInfo().PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing; }
        catch { return false; }
    }

    private void DetachSession()
    {
        var s = _session;
        if (s is null) return;
        try
        {
            s.MediaPropertiesChanged -= OnMediaPropertiesChanged;
            s.PlaybackInfoChanged -= OnPlaybackInfoChanged;
            s.TimelinePropertiesChanged -= OnTimelineChanged;
        }
        catch (Exception ex) { Log.Debug("Detaching media session handlers failed: " + ex.Message); }
        _session = null;
    }

    private void OnMediaPropertiesChanged(GlobalSystemMediaTransportControlsSession s, MediaPropertiesChangedEventArgs a) => ScheduleRefresh(120);
    private void OnPlaybackInfoChanged(GlobalSystemMediaTransportControlsSession s, PlaybackInfoChangedEventArgs a) => ScheduleRefresh(60);
    private void OnTimelineChanged(GlobalSystemMediaTransportControlsSession s, TimelinePropertiesChangedEventArgs a) => ScheduleRefresh(250);

    private void ScheduleRefresh(int delayMs)
    {
        CancellationTokenSource cts;
        lock (_sync)
        {
            if (_disposed) return;
            try { _debounce?.Cancel(); } catch (ObjectDisposedException) { }
            _debounce = cts = new CancellationTokenSource();
        }
        _ = Task.Run(async () =>
        {
            try
            {
                var token = cts.Token;
                if (delayMs > 0) await Task.Delay(delayMs, token).ConfigureAwait(false);
                await RefreshAsync(token).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is OperationCanceledException or ObjectDisposedException) { }
            catch (Exception ex) { Log.Warn("Media refresh failed.", ex); }
        });
    }

    private async Task RefreshAsync(CancellationToken ct)
    {
        await _refreshGate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var session = _session;
            if (session is null) { Publish(MediaInfo.None); return; }

            MediaInfo info;
            try
            {
                var props = await session.TryGetMediaPropertiesAsync();
                ct.ThrowIfCancellationRequested();
                var playback = session.GetPlaybackInfo();
                var timeline = session.GetTimelineProperties();
                var controls = playback.Controls;
                string appId = session.SourceAppUserModelId ?? "";

                var reportedPos = timeline.Position - timeline.StartTime;
                TimeSpan finalPos = reportedPos;
                DateTimeOffset finalTimestamp = timeline.LastUpdatedTime;

                if (finalTimestamp == default || finalTimestamp == DateTimeOffset.MinValue || finalTimestamp > DateTimeOffset.Now.AddSeconds(2))
                {
                    finalTimestamp = DateTimeOffset.Now;
                }

                if (DateTimeOffset.Now < _seekSuppressionUntil && Current.Duration > TimeSpan.Zero)
                {
                    if (Math.Abs((reportedPos - Current.Position).TotalSeconds) < 1.0)
                    {
                        _seekSuppressionUntil = DateTimeOffset.MinValue;
                        finalPos = reportedPos;
                        finalTimestamp = DateTimeOffset.Now;
                    }
                    else
                    {
                        finalPos = Current.EstimatePosition(DateTimeOffset.Now);
                        finalTimestamp = DateTimeOffset.Now;
                    }
                }

                info = new MediaInfo
                {
                    HasSession = true,
                    SourceAppId = appId,
                    AppName = MediaInfo.FriendlyAppName(appId),
                    Title = props?.Title?.Trim() ?? "",
                    Artist = (props?.Artist ?? props?.AlbumArtist ?? "").Trim(),
                    Album = props?.AlbumTitle?.Trim() ?? "",
                    IsPlaying = playback.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing,
                    CanPlayPause = controls.IsPlayPauseToggleEnabled || controls.IsPlayEnabled || controls.IsPauseEnabled,
                    CanNext = controls.IsNextEnabled,
                    CanPrevious = controls.IsPreviousEnabled,
                    Position = finalPos,
                    Duration = timeline.EndTime - timeline.StartTime,
                    PositionTimestamp = finalTimestamp,
                    PlaybackRate = playback.PlaybackRate ?? 1.0,
                };

                var thumbnail = await GetArtworkAsync(info.TrackKey, props?.Thumbnail, ct).ConfigureAwait(false);
                info = info with { Thumbnail = thumbnail };
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                // The source app most likely closed between the event and our query.
                Log.Debug("Media session query failed: " + ex.GetType().Name);
                info = MediaInfo.None;
            }

            Publish(info);
        }
        finally
        {
            _refreshGate.Release();
        }
    }

    private void Publish(MediaInfo info)
    {
        if (Equals(info, Current)) return;
        Current = info;
        try { MediaChanged?.Invoke(info); }
        catch (Exception ex) { Log.Error("MediaChanged handler threw.", ex); }
    }

    // ------------------------------------------------------------------ Artwork

    private async Task<BitmapSource?> GetArtworkAsync(string key, IRandomAccessStreamReference? reference, CancellationToken ct)
    {
        lock (_artCache)
        {
            var node = _artCache.First;
            while (node is not null)
            {
                if (node.Value.Key == key)
                {
                    // Cache a null (no artwork) only if the source still provides none.
                    if (node.Value.Image is not null || reference is null)
                    {
                        _artCache.Remove(node);
                        _artCache.AddFirst(node);
                        return node.Value.Image;
                    }
                    _artCache.Remove(node);
                    break;
                }
                node = node.Next;
            }
        }

        BitmapSource? image = null;
        if (reference is not null)
        {
            try
            {
                using var winrtStream = await reference.OpenReadAsync();
                ct.ThrowIfCancellationRequested();
                if (winrtStream.Size is > 0 and <= MaxArtworkBytes)
                {
                    using var source = winrtStream.AsStreamForRead();
                    var ms = new MemoryStream((int)winrtStream.Size);
                    await source.CopyToAsync(ms, ct).ConfigureAwait(false);
                    ms.Position = 0;
                    image = Decode(ms);
                }
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                Log.Debug("Artwork could not be loaded: " + ex.GetType().Name);
            }
        }

        lock (_artCache)
        {
            _artCache.AddFirst((key, image));
            while (_artCache.Count > ArtworkCacheSize) _artCache.RemoveLast();
        }
        return image;
    }

    private static BitmapSource? Decode(MemoryStream ms)
    {
        try
        {
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
            bmp.DecodePixelWidth = ArtworkDecodeSize;
            bmp.StreamSource = ms;
            bmp.EndInit();
            bmp.Freeze();
            return bmp;
        }
        catch (Exception ex)
        {
            Log.Debug("Artwork decode failed: " + ex.GetType().Name);
            return null;
        }
        finally
        {
            ms.Dispose();
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        Stop();
        _disposed = true;
        lock (_artCache) _artCache.Clear();
    }
}
