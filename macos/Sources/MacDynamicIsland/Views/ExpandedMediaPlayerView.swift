import SwiftUI
import AppKit

/// Expanded media player view matching Windows Edition layout
/// (Artwork 52x52, Title/Artist/App, Lyrics toggle, Interactive scrubber, Synced lyrics card, Playback controls)
public struct ExpandedMediaPlayerView: View {
    @EnvironmentObject private var mediaService: MediaService
    @EnvironmentObject private var lyricsService: LyricsService

    @Binding var expansionState: IslandExpansionState
    @State private var isDraggingScrubber: Bool = false
    @State private var scrubPosition: Double = 0.0
    @State private var isLyricsFullView: Bool = false

    public init(expansionState: Binding<IslandExpansionState>) {
        self._expansionState = expansionState
    }

    public var body: some View {
        VStack(spacing: 8) {
            // Row 1: Artwork + Song Details + Lyrics Mode Toggle Button
            HStack(spacing: 12) {
                // Album Artwork (52x52 rounded squircle with drop shadow)
                ZStack {
                    RoundedRectangle(cornerRadius: 10)
                        .fill(Color.white.opacity(0.08))
                        .frame(width: 52, height: 52)
                        .shadow(color: Color.black.opacity(0.45), radius: 8, x: 0, y: 3)

                    if let art = mediaService.currentTrack.artwork {
                        Image(nsImage: art)
                            .resizable()
                            .scaledToFill()
                            .frame(width: 52, height: 52)
                            .clipShape(RoundedRectangle(cornerRadius: 10))
                    } else {
                        Image(systemName: "music.note")
                            .font(.system(size: 22, weight: .semibold))
                            .foregroundColor(Color.white.opacity(0.35))
                    }
                }

                // Title, Artist, Source App
                VStack(alignment: .leading, spacing: 2) {
                    Text(mediaService.currentTrack.title.isEmpty ? "No Media Playing" : mediaService.currentTrack.title)
                        .font(.system(size: 13.5, weight: .bold, design: .rounded))
                        .foregroundColor(.white)
                        .lineLimit(1)

                    Text(mediaService.currentTrack.artist.isEmpty ? "Start playback on Spotify, YouTube, or Music" : mediaService.currentTrack.artist)
                        .font(.system(size: 11, weight: .regular, design: .rounded))
                        .foregroundColor(.white.opacity(0.70))
                        .lineLimit(1)

                    Text(mediaService.currentTrack.sourceApp.isEmpty ? "Mac Dynamic Island" : mediaService.currentTrack.sourceApp)
                        .font(.system(size: 9.5, weight: .medium, design: .rounded))
                        .foregroundColor(.white.opacity(0.40))
                        .lineLimit(1)
                }

                Spacer()

                // Lyrics Mode Toggle Button (Yellow Microphone)
                Button(action: {
                    withAnimation(.spring(response: 0.35, dampingFraction: 0.75)) {
                        isLyricsFullView.toggle()
                    }
                }) {
                    ZStack {
                        RoundedRectangle(cornerRadius: 8)
                            .fill(isLyricsFullView ? Color(hex: "#FFCC00").opacity(0.20) : Color.white.opacity(0.08))
                            .frame(width: 32, height: 32)
                            .overlay(
                                RoundedRectangle(cornerRadius: 8)
                                    .stroke(isLyricsFullView ? Color(hex: "#FFCC00").opacity(0.4) : Color.clear, lineWidth: 1)
                            )

                        Text("🎤")
                            .font(.system(size: 13))
                    }
                }
                .buttonStyle(.plain)
                .help(isLyricsFullView ? "Tampilkan Lirik Ringkas" : "Tampilkan Lirik Karaoke Lengkap")
            }
            .padding(.horizontal, 16)
            .padding(.top, 4)

            // Row 2: Interactive Timeline Scrubber & Timestamps
            VStack(spacing: 3) {
                GeometryReader { geo in
                    let current = isDraggingScrubber ? scrubPosition : mediaService.currentTrack.position
                    let total = max(mediaService.currentTrack.duration, 1)
                    let progress = min(max(current / total, 0), 1)

                    ZStack(alignment: .leading) {
                        // Background Track
                        Capsule()
                            .fill(Color.white.opacity(0.18))
                            .frame(height: 4)

                        // Filled Track (Yellow accent like Windows edition)
                        Capsule()
                            .fill(Color(hex: "#FFCC00"))
                            .frame(width: geo.size.width * progress, height: 4)

                        // Scrubber Thumb Dot (10x10 with shadow)
                        Circle()
                            .fill(Color.white)
                            .frame(width: 10, height: 10)
                            .shadow(color: Color.black.opacity(0.5), radius: 3, x: 0, y: 1)
                            .offset(x: max(0, min(geo.size.width - 10, (geo.size.width * progress) - 5)))
                    }
                    .frame(height: 16)
                    .contentShape(Rectangle())
                    .gesture(
                        DragGesture(minimumDistance: 0)
                            .onChanged { val in
                                isDraggingScrubber = true
                                let clampedX = min(max(val.location.x, 0), geo.size.width)
                                let pct = clampedX / geo.size.width
                                scrubPosition = pct * total
                            }
                            .onEnded { val in
                                isDraggingScrubber = false
                                let clampedX = min(max(val.location.x, 0), geo.size.width)
                                let pct = clampedX / geo.size.width
                                let target = pct * total
                                mediaService.seek(to: target)
                            }
                    )
                }
                .frame(height: 16)

                // Timestamps
                HStack {
                    Text(isDraggingScrubber ? formatSeconds(scrubPosition) : mediaService.currentTrack.formattedPosition)
                        .font(.system(size: 10, weight: .regular, design: .monospaced))
                        .foregroundColor(.white.opacity(0.5))

                    Spacer()

                    Text(mediaService.currentTrack.formattedDuration)
                        .font(.system(size: 10, weight: .regular, design: .monospaced))
                        .foregroundColor(.white.opacity(0.5))
                }
            }
            .padding(.horizontal, 16)

            // Row 3: Live Synchronized Lyrics Card with Click-to-Seek
            ZStack {
                RoundedRectangle(cornerRadius: 10)
                    .fill(Color.white.opacity(0.08))

                if !isLyricsFullView {
                    // View A: Compact Single-Line Live Ticker
                    HStack(spacing: 8) {
                        Text("🎵")
                            .font(.system(size: 11))
                            .foregroundColor(Color(hex: "#FFCC00"))

                        Text(currentLyricText)
                            .font(.system(size: 11.5, weight: .semibold))
                            .foregroundColor(.white)
                            .lineLimit(1)

                        Spacer()
                    }
                    .padding(.horizontal, 10)
                    .padding(.vertical, 6)
                    .contentShape(Rectangle())
                    .onTapGesture {
                        withAnimation(.spring(response: 0.35, dampingFraction: 0.75)) {
                            isLyricsFullView.toggle()
                        }
                    }
                } else {
                    // View B: Multi-Line Karaoke Stream (Apple Music Sing style)
                    VStack(spacing: 3) {
                        // Previous line
                        if let prev = previousLyric {
                            Button(action: {
                                mediaService.seek(to: prev.timestamp)
                            }) {
                                Text(prev.text)
                                    .font(.system(size: 10, weight: .medium))
                                    .foregroundColor(.white.opacity(0.40))
                                    .lineLimit(1)
                                    .frame(maxWidth: .infinity)
                            }
                            .buttonStyle(.plain)
                            .help("Lompat mundur ke lirik sebelumnya")
                        }

                        // Current line
                        Button(action: {
                            if let cur = currentLyricLine {
                                mediaService.seek(to: cur.timestamp)
                            }
                        }) {
                            Text(currentLyricText)
                                .font(.system(size: 12, weight: .bold))
                                .foregroundColor(Color(hex: "#FFCC00"))
                                .lineLimit(1)
                                .padding(.horizontal, 8)
                                .padding(.vertical, 2)
                                .background(Color.white.opacity(0.10))
                                .cornerRadius(5)
                                .frame(maxWidth: .infinity)
                        }
                        .buttonStyle(.plain)
                        .help("Ulangi baris lirik saat ini")

                        // Next line
                        if let next = nextLyric {
                            Button(action: {
                                mediaService.seek(to: next.timestamp)
                            }) {
                                Text(next.text)
                                    .font(.system(size: 10, weight: .medium))
                                    .foregroundColor(.white.opacity(0.45))
                                    .lineLimit(1)
                                    .frame(maxWidth: .infinity)
                            }
                            .buttonStyle(.plain)
                            .help("Lompat maju ke lirik ini")
                        }
                    }
                    .padding(.horizontal, 8)
                    .padding(.vertical, 4)
                }
            }
            .frame(height: isLyricsFullView ? 62 : 28)
            .padding(.horizontal, 16)
            .animation(.spring(response: 0.32, dampingFraction: 0.75), value: isLyricsFullView)

            // Row 4: Playback Controls (Previous, Play/Pause, Next)
            HStack(spacing: 24) {
                // Previous Track Button
                Button(action: { mediaService.previousTrack() }) {
                    Image(systemName: "backward.fill")
                        .font(.system(size: 15, weight: .medium))
                        .foregroundColor(.white.opacity(0.85))
                        .frame(width: 36, height: 36)
                        .background(Color.white.opacity(0.08))
                        .clipShape(Circle())
                }
                .buttonStyle(.plain)
                .help("Lagu Sebelumnya")

                // Play / Pause Button
                Button(action: { mediaService.togglePlayPause() }) {
                    ZStack {
                        Circle()
                            .fill(Color.white.opacity(0.20))
                            .frame(width: 44, height: 44)

                        Image(systemName: mediaService.currentTrack.isPlaying ? "pause.fill" : "play.fill")
                            .font(.system(size: 17, weight: .bold))
                            .foregroundColor(.white)
                            .offset(x: mediaService.currentTrack.isPlaying ? 0 : 1)
                    }
                }
                .buttonStyle(.plain)
                .help("Play / Pause")

                // Next Track Button
                Button(action: { mediaService.nextTrack() }) {
                    Image(systemName: "forward.fill")
                        .font(.system(size: 15, weight: .medium))
                        .foregroundColor(.white.opacity(0.85))
                        .frame(width: 36, height: 36)
                        .background(Color.white.opacity(0.08))
                        .clipShape(Circle())
                }
                .buttonStyle(.plain)
                .help("Lagu Berikutnya")
            }
            .padding(.top, 2)
            .padding(.bottom, 6)
        }
    }

    // MARK: - Lyrics Helpers
    private var currentLyricLine: LyricLine? {
        guard lyricsService.lines.indices.contains(lyricsService.activeIndex) else { return nil }
        return lyricsService.lines[lyricsService.activeIndex]
    }

    private var previousLyric: LyricLine? {
        let prevIdx = lyricsService.activeIndex - 1
        guard prevIdx >= 0 && lyricsService.lines.indices.contains(prevIdx) else { return nil }
        return lyricsService.lines[prevIdx]
    }

    private var nextLyric: LyricLine? {
        let nextIdx = lyricsService.activeIndex + 1
        guard lyricsService.lines.indices.contains(nextIdx) else { return nil }
        return lyricsService.lines[nextIdx]
    }

    private var currentLyricText: String {
        if let line = currentLyricLine, !line.text.isEmpty {
            return line.text
        }
        if lyricsService.isLoading {
            return "Memuat lirik karaoke..."
        }
        if lyricsService.hasLyrics {
            return "🎵 Memutar musik..."
        }
        return "🎵 Menunggu pemutaran musik..."
    }

    private func formatSeconds(_ sec: Double) -> String {
        let s = Int(sec)
        let mins = s / 60
        let secs = s % 60
        return String(format: "%02d:%02d", mins, secs)
    }
}
