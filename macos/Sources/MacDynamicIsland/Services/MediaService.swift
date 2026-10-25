import Foundation
import AppKit

/// Service for detecting and controlling media playback on macOS (Spotify, Apple Music, etc.)
@MainActor
public final class MediaService: ObservableObject {
    public static let shared = MediaService()

    @Published public private(set) var currentTrack = MediaTrack()
    @Published public private(set) var isMediaAvailable: Bool = false

    private var pollTimer: Timer?
    private var seekLockUntil: Date = .distantPast

    public init() {
        startPolling()
    }

    public func startPolling() {
        pollTimer?.invalidate()
        // 100ms interval for fluid scrubbing and lyrics synchronization
        pollTimer = Timer.scheduledTimer(withTimeInterval: 0.1, repeats: true) { [weak self] _ in
            Task { @MainActor [weak self] in
                self?.pollActivePlayer()
            }
        }
    }

    public func pollActivePlayer() {
        // Check Spotify first, then Apple Music
        if isRunning("com.spotify.client") {
            pollSpotify()
        } else if isRunning("com.apple.Music") {
            pollAppleMusic()
        } else {
            if isMediaAvailable {
                isMediaAvailable = false
                currentTrack = MediaTrack()
            }
        }
    }

    private func isRunning(_ bundleId: String) -> Bool {
        !NSRunningApplication.runningApplications(withBundleIdentifier: bundleId).isEmpty
    }

    // MARK: - Spotify AppleScript
    private func pollSpotify() {
        let script = """
        if application "Spotify" is running then
            tell application "Spotify"
                set t_state to player state as string
                set t_name to name of current track
                set t_artist to artist of current track
                set t_album to album of current track
                set t_pos to player position
                set t_dur to (duration of current track) / 1000
                return t_state & "|||" & t_name & "|||" & t_artist & "|||" & t_album & "|||" & (t_pos as string) & "|||" & (t_dur as string)
            end tell
        else
            return "stopped"
        end if
        """

        guard let output = runAppleScript(script), !output.isEmpty, output != "stopped" else {
            isMediaAvailable = false
            return
        }

        let parts = output.components(separatedBy: "|||")
        guard parts.count >= 6 else { return }

        let state = parts[0]
        let title = parts[1]
        let artist = parts[2]
        let album = parts[3]
        let pos = Double(parts[4]) ?? 0
        let dur = Double(parts[5]) ?? 0

        let isPlaying = (state.lowercased() == "playing")

        // Only update position if we are not in a temporary seek-lock window
        let finalPos = Date() < seekLockUntil ? currentTrack.position : pos

        let hasChangedSong = (currentTrack.title != title || currentTrack.artist != artist)

        currentTrack = MediaTrack(
            title: title,
            artist: artist,
            album: album,
            isPlaying: isPlaying,
            position: finalPos,
            duration: dur,
            artwork: currentTrack.artwork,
            sourceApp: "Spotify"
        )
        isMediaAvailable = !title.isEmpty

        if hasChangedSong {
            Task {
                await LyricsService.shared.fetchLyrics(trackTitle: title, artist: artist, duration: dur)
            }
        } else {
            LyricsService.shared.updatePosition(finalPos)
        }
    }

    // MARK: - Apple Music AppleScript
    private func pollAppleMusic() {
        let script = """
        if application "Music" is running then
            tell application "Music"
                set t_state to player state as string
                if t_state is not "stopped" then
                    set t_name to name of current track
                    set t_artist to artist of current track
                    set t_album to album of current track
                    set t_pos to player position
                    set t_dur to duration of current track
                    return t_state & "|||" & t_name & "|||" & t_artist & "|||" & t_album & "|||" & (t_pos as string) & "|||" & (t_dur as string)
                else
                    return "stopped"
                end if
            end tell
        else
            return "stopped"
        end if
        """

        guard let output = runAppleScript(script), !output.isEmpty, output != "stopped" else {
            isMediaAvailable = false
            return
        }

        let parts = output.components(separatedBy: "|||")
        guard parts.count >= 6 else { return }

        let state = parts[0]
        let title = parts[1]
        let artist = parts[2]
        let album = parts[3]
        let pos = Double(parts[4]) ?? 0
        let dur = Double(parts[5]) ?? 0

        let isPlaying = (state.lowercased() == "playing")
        let finalPos = Date() < seekLockUntil ? currentTrack.position : pos

        let hasChangedSong = (currentTrack.title != title || currentTrack.artist != artist)

        currentTrack = MediaTrack(
            title: title,
            artist: artist,
            album: album,
            isPlaying: isPlaying,
            position: finalPos,
            duration: dur,
            artwork: currentTrack.artwork,
            sourceApp: "Music"
        )
        isMediaAvailable = !title.isEmpty

        if hasChangedSong {
            Task {
                await LyricsService.shared.fetchLyrics(trackTitle: title, artist: artist, duration: dur)
            }
        } else {
            LyricsService.shared.updatePosition(finalPos)
        }
    }

    // MARK: - Playback Controls
    public func togglePlayPause() {
        if currentTrack.sourceApp == "Spotify" {
            _ = runAppleScript("tell application \"Spotify\" to playpause")
        } else {
            _ = runAppleScript("tell application \"Music\" to playpause")
        }
        currentTrack.isPlaying.toggle()
    }

    public func nextTrack() {
        if currentTrack.sourceApp == "Spotify" {
            _ = runAppleScript("tell application \"Spotify\" to next track")
        } else {
            _ = runAppleScript("tell application \"Music\" to next track")
        }
    }

    public func previousTrack() {
        if currentTrack.sourceApp == "Spotify" {
            _ = runAppleScript("tell application \"Spotify\" to previous track")
        } else {
            _ = runAppleScript("tell application \"Music\" to previous track")
        }
    }

    public func seek(to seconds: Double) {
        seekLockUntil = Date().addingTimeInterval(1.5)
        currentTrack.position = seconds
        LyricsService.shared.updatePosition(seconds)

        if currentTrack.sourceApp == "Spotify" {
            _ = runAppleScript("tell application \"Spotify\" to set player position to \(seconds)")
        } else {
            _ = runAppleScript("tell application \"Music\" to set player position to \(seconds)")
        }
    }

    private func runAppleScript(_ script: String) -> String? {
        var error: NSDictionary?
        if let scriptObject = NSAppleScript(source: script) {
            let output = scriptObject.executeAndReturnError(&error)
            if error == nil {
                return output.stringValue
            }
        }
        return nil
    }
}
