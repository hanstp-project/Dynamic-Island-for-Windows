import Foundation
import AppKit

public struct MediaTrack: Equatable {
    public var title: String
    public var artist: String
    public var album: String
    public var isPlaying: Bool
    public var position: Double       // current seconds
    public var duration: Double       // total seconds
    public var artwork: NSImage?
    public var sourceApp: String      // e.g. "Spotify", "Music", "Google Chrome"

    public init(
        title: String = "",
        artist: String = "",
        album: String = "",
        isPlaying: Bool = false,
        position: Double = 0,
        duration: Double = 0,
        artwork: NSImage? = nil,
        sourceApp: String = "Music"
    ) {
        self.title = title
        self.artist = artist
        self.album = album
        self.isPlaying = isPlaying
        self.position = position
        self.duration = duration
        self.artwork = artwork
        self.sourceApp = sourceApp
    }

    public var formattedPosition: String {
        formatTime(position)
    }

    public var formattedDuration: String {
        formatTime(duration)
    }

    public var progress: Double {
        guard duration > 0 else { return 0 }
        return min(max(position / duration, 0), 1)
    }

    private func formatTime(_ seconds: Double) -> String {
        let s = Int(seconds)
        let mins = s / 60
        let secs = s % 60
        return String(format: "%02d:%02d", mins, secs)
    }
}
