using System;
using System.Windows.Media.Imaging;

namespace WindowsDynamicIsland.Models;

public enum NotificationKind { Info, Media, Battery, Network, System, Windows }

/// <summary>A notification presented by the island.</summary>
public sealed record NotificationInfo
{
    private static long _nextId;

    public long Id { get; } = System.Threading.Interlocked.Increment(ref _nextId);
    public NotificationKind Kind { get; init; } = NotificationKind.Info;
    public string Title { get; init; } = "";
    public string Message { get; init; } = "";
    public string? Details { get; init; }
    public string? AppName { get; init; }
    /// <summary>Segoe Fluent/MDL2 glyph shown when no image is supplied.</summary>
    public string IconGlyph { get; init; } = "\uE946";
    /// <summary>Optional accent colour (hex) for the icon badge.</summary>
    public string AccentColor { get; init; } = "#5B6CFF";
    public BitmapSource? Image { get; init; }
    /// <summary>Null = use the user's configured timeout.</summary>
    public TimeSpan? Timeout { get; init; }
    /// <summary>Notifications with the same key replace / de-duplicate each other.</summary>
    public string? DedupeKey { get; init; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.Now;

    public bool HasDetails => !string.IsNullOrWhiteSpace(Details);
}
