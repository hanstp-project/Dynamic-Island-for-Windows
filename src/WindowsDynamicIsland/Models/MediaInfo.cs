using System;
using System.Windows.Media.Imaging;

namespace WindowsDynamicIsland.Models;

/// <summary>Immutable snapshot of the current media session.</summary>
public sealed record MediaInfo
{
    public static readonly MediaInfo None = new();

    public bool HasSession { get; init; }
    public string SourceAppId { get; init; } = "";
    public string AppName { get; init; } = "";
    public string Title { get; init; } = "";
    public string Artist { get; init; } = "";
    public string Album { get; init; } = "";
    public bool IsPlaying { get; init; }
    public bool CanPlayPause { get; init; }
    public bool CanNext { get; init; }
    public bool CanPrevious { get; init; }
    public TimeSpan Position { get; init; }
    public TimeSpan Duration { get; init; }
    /// <summary>When <see cref="Position"/> was sampled by the source app (used for interpolation).</summary>
    public DateTimeOffset PositionTimestamp { get; init; }
    public double PlaybackRate { get; init; } = 1.0;
    public BitmapSource? Thumbnail { get; init; }

    /// <summary>Identity of the current track (used for change detection and artwork cache).</summary>
    public string TrackKey => $"{SourceAppId}|{Title}|{Artist}|{Album}";

    public bool HasTimeline => Duration > TimeSpan.FromSeconds(1);

    /// <summary>Best estimate of the current position, interpolated while playing.</summary>
    public TimeSpan EstimatePosition(DateTimeOffset now)
    {
        if (!HasTimeline) return TimeSpan.Zero;
        var pos = Position;
        if (IsPlaying && PositionTimestamp != default)
        {
            var elapsed = now - PositionTimestamp;
            if (elapsed > TimeSpan.Zero && elapsed < TimeSpan.FromHours(6))
                pos += TimeSpan.FromTicks((long)(elapsed.Ticks * Math.Clamp(PlaybackRate, 0, 8)));
        }
        if (pos < TimeSpan.Zero) pos = TimeSpan.Zero;
        if (pos > Duration) pos = Duration;
        return pos;
    }

    /// <summary>Converts an AUMID or exe name ("Spotify.exe", "AppleInc.AppleMusicWin_nzyj5cx40ttqa!App") into a short display name.</summary>
    public static string FriendlyAppName(string? sourceAppId)
    {
        if (string.IsNullOrWhiteSpace(sourceAppId)) return "Media";
        var raw = sourceAppId.Trim();
        if (raw.Contains("AppleMusic", StringComparison.OrdinalIgnoreCase) || raw.Contains("Apple.Music", StringComparison.OrdinalIgnoreCase))
            return "Apple Music";
        if (raw.Contains("Spotify", StringComparison.OrdinalIgnoreCase))
            return "Spotify";
        if (raw.Contains("TIDAL", StringComparison.OrdinalIgnoreCase))
            return "TIDAL";
        if (raw.Contains("AmazonMusic", StringComparison.OrdinalIgnoreCase))
            return "Amazon Music";
        if (raw.Contains("Deezer", StringComparison.OrdinalIgnoreCase))
            return "Deezer";
        if (raw.Contains("ZuneMusic", StringComparison.OrdinalIgnoreCase) || raw.Contains("Media Player", StringComparison.OrdinalIgnoreCase))
            return "Media Player";
        if (raw.Contains("msedge", StringComparison.OrdinalIgnoreCase))
            return "Microsoft Edge";
        if (raw.Contains("chrome", StringComparison.OrdinalIgnoreCase))
            return "Google Chrome";
        if (raw.Contains("firefox", StringComparison.OrdinalIgnoreCase))
            return "Firefox";

        var id = raw;
        int bang = id.IndexOf('!');
        if (bang >= 0) id = id[(bang + 1)..];
        if (id.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) id = id[..^4];
        int dot = id.LastIndexOf('.');
        if (dot >= 0 && dot < id.Length - 1) id = id[(dot + 1)..];
        id = id switch
        {
            "ZuneMusic" => "Media Player",
            "ZuneVideo" => "Films & TV",
            "msedge" => "Microsoft Edge",
            "chrome" => "Google Chrome",
            "firefox" => "Firefox",
            "Spotify" or "spotify" => "Spotify",
            _ => id,
        };
        return id.Length == 0 ? "Media" : char.ToUpperInvariant(id[0]) + id[1..];
    }
}
