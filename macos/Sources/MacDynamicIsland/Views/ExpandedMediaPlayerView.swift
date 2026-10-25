import SwiftUI

/// Expanded media player view with cover art, playback controls, and duration scrubber
public struct ExpandedMediaPlayerView: View {
    @EnvironmentObject private var mediaService: MediaService
    @EnvironmentObject private var lyricsService: LyricsService

    @Binding var expansionState: IslandExpansionState
    @State private var isDraggingScrubber: Bool = false
    @State private var scrubPosition: Double = 0.0

    public init(expansionState: Binding<IslandExpansionState>) {
        self._expansionState = expansionState
    }

    public var body: some View {
        VStack(spacing: 12) {
            // Top Row: Album art, track info, lyrics toggle
            HStack(spacing: 14) {
                // Album Art
                ZStack {
                    RoundedRectangle(cornerRadius: 12)
                        .fill(LinearGradient(
                            colors: [Color.pink.opacity(0.8), Color.purple.opacity(0.8)],
                            startPoint: .topLeading,
                            endPoint: .bottomTrailing
                        ))
                        .frame(width: 54, height: 54)
                        .shadow(color: Color.pink.opacity(0.3), radius: 8, x: 0, y: 4)

                    Image(systemName: "music.note")
                        .font(.system(size: 24, weight: .semibold))
                        .foregroundColor(.white)
                }

                // Title and Artist
                VStack(alignment: .leading, spacing: 3) {
                    Text(mediaService.currentTrack.title.isEmpty ? "No Media Playing" : mediaService.currentTrack.title)
                        .font(.system(size: 15, weight: .bold, design: .rounded))
                        .foregroundColor(.white)
                        .lineLimit(1)

                    Text(mediaService.currentTrack.artist.isEmpty ? "Mac Audio Session" : mediaService.currentTrack.artist)
                        .font(.system(size: 13, weight: .medium, design: .rounded))
                        .foregroundColor(.white.opacity(0.7))
                        .lineLimit(1)

                    Text(mediaService.currentTrack.sourceApp)
                        .font(.system(size: 10, weight: .semibold, design: .rounded))
                        .foregroundColor(.white.opacity(0.4))
                }

                Spacer()

                // Lyrics Mode Toggle Button
                Button(action: {
                    withAnimation(.spring(response: 0.35, dampingFraction: 0.75)) {
                        expansionState = .lyricsExpanded
                    }
                }) {
                    ZStack {
                        RoundedRectangle(cornerRadius: 10)
                            .fill(lyricsService.hasLyrics ? Color.white.opacity(0.18) : Color.white.opacity(0.08))
                            .frame(width: 38, height: 38)

                        Image(systemName: "quote.bubble.fill")
                            .font(.system(size: 16, weight: .semibold))
                            .foregroundColor(lyricsService.hasLyrics ? .white : .white.opacity(0.4))
                    }
                }
                .buttonStyle(.plain)
                .help("Tampilkan Lirik Karaoke")
            }

            // Middle: Interactive Progress Scrubber
            VStack(spacing: 4) {
                GeometryReader { geo in
                    let current = isDraggingScrubber ? scrubPosition : mediaService.currentTrack.position
                    let total = max(mediaService.currentTrack.duration, 1)
                    let progress = min(max(current / total, 0), 1)

                    ZStack(alignment: .leading) {
                        // Background track
                        Capsule()
                            .fill(Color.white.opacity(0.18))
                            .frame(height: 6)

                        // Filled active track
                        Capsule()
                            .fill(LinearGradient(
                                colors: [Color.pink, Color.orange],
                                startPoint: .leading,
                                endPoint: .trailing
                            ))
                            .frame(width: geo.size.width * progress, height: 6)

                        // Scrubber thumb
                        Circle()
                            .fill(Color.white)
                            .frame(width: 14, height: 14)
                            .shadow(radius: 3)
                            .offset(x: (geo.size.width * progress) - 7)
                    }
                    .frame(height: 18)
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
                .frame(height: 18)

                // Time labels
                HStack {
                    Text(isDraggingScrubber ? formatSeconds(scrubPosition) : mediaService.currentTrack.formattedPosition)
                        .font(.system(size: 10, weight: .medium, design: .monospaced))
                        .foregroundColor(.white.opacity(0.5))

                    Spacer()

                    Text(mediaService.currentTrack.formattedDuration)
                        .font(.system(size: 10, weight: .medium, design: .monospaced))
                        .foregroundColor(.white.opacity(0.5))
                }
            }

            // Bottom: Controls (Previous, Play/Pause, Next)
            HStack(spacing: 28) {
                Button(action: { mediaService.previousTrack() }) {
                    Image(systemName: "backward.fill")
                        .font(.system(size: 18, weight: .medium))
                        .foregroundColor(.white.opacity(0.85))
                }
                .buttonStyle(.plain)

                Button(action: { mediaService.togglePlayPause() }) {
                    ZStack {
                        Circle()
                            .fill(Color.white)
                            .frame(width: 44, height: 44)
                            .shadow(color: Color.black.opacity(0.2), radius: 6)

                        Image(systemName: mediaService.currentTrack.isPlaying ? "pause.fill" : "play.fill")
                            .font(.system(size: 20, weight: .bold))
                            .foregroundColor(.black)
                            .offset(x: mediaService.currentTrack.isPlaying ? 0 : 1.5)
                    }
                }
                .buttonStyle(.plain)

                Button(action: { mediaService.nextTrack() }) {
                    Image(systemName: "forward.fill")
                        .font(.system(size: 18, weight: .medium))
                        .foregroundColor(.white.opacity(0.85))
                }
                .buttonStyle(.plain)
            }
            .padding(.top, 2)
        }
        .padding(18)
    }

    private func formatSeconds(_ sec: Double) -> String {
        let s = Int(sec)
        let mins = s / 60
        let secs = s % 60
        return String(format: "%02d:%02d", mins, secs)
    }
}
