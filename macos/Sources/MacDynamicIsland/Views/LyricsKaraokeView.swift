import SwiftUI
import Combine

/// Apple Music Sing style synchronized lyrics with interactive Click-to-Seek
public struct LyricsKaraokeView: View {
    @EnvironmentObject private var mediaService: MediaService
    @EnvironmentObject private var lyricsService: LyricsService

    @Binding var expansionState: IslandExpansionState

    public init(expansionState: Binding<IslandExpansionState>) {
        self._expansionState = expansionState
    }

    public var body: some View {
        VStack(spacing: 8) {
            // Header: Back button, track info
            HStack {
                Button(action: {
                    withAnimation(.spring(response: 0.35, dampingFraction: 0.75)) {
                        expansionState = .mediaExpanded
                    }
                }) {
                    HStack(spacing: 4) {
                        Image(systemName: "chevron.left")
                            .font(.system(size: 13, weight: .bold))
                        Text("Player")
                            .font(.system(size: 12, weight: .semibold, design: .rounded))
                    }
                    .foregroundColor(.white.opacity(0.8))
                    .padding(.horizontal, 10)
                    .padding(.vertical, 5)
                    .background(Color.white.opacity(0.12))
                    .clipShape(Capsule())
                }
                .buttonStyle(.plain)

                Spacer()

                VStack(spacing: 1) {
                    Text(mediaService.currentTrack.title)
                        .font(.system(size: 12, weight: .bold, design: .rounded))
                        .foregroundColor(.white)
                        .lineLimit(1)
                    Text(mediaService.currentTrack.artist)
                        .font(.system(size: 10, weight: .medium, design: .rounded))
                        .foregroundColor(.white.opacity(0.6))
                        .lineLimit(1)
                }

                Spacer()

                // Active Time
                Text(mediaService.currentTrack.formattedPosition)
                    .font(.system(size: 11, weight: .semibold, design: .monospaced))
                    .foregroundColor(.white.opacity(0.7))
                    .padding(.horizontal, 8)
                    .padding(.vertical, 4)
                    .background(Color.white.opacity(0.1))
                    .clipShape(Capsule())
            }
            .padding(.horizontal, 14)
            .padding(.top, 12)

            Divider()
                .background(Color.white.opacity(0.1))

            // Lyrics ScrollView
            if lyricsService.isLoading {
                VStack(spacing: 8) {
                    ProgressView()
                        .scaleEffect(0.8)
                    Text("Mengambil lirik dari LRCLIB...")
                        .font(.system(size: 12, weight: .medium, design: .rounded))
                        .foregroundColor(.white.opacity(0.5))
                }
                .frame(maxHeight: .infinity)
            } else if lyricsService.lines.isEmpty {
                VStack(spacing: 6) {
                    Image(systemName: "music.mic")
                        .font(.system(size: 26))
                        .foregroundColor(.white.opacity(0.3))
                    Text("Lirik sinkron tidak tersedia untuk lagu ini")
                        .font(.system(size: 12, weight: .medium, design: .rounded))
                        .foregroundColor(.white.opacity(0.5))
                }
                .frame(maxHeight: .infinity)
            } else {
                ScrollViewReader { proxy in
                    ScrollView(.vertical, showsIndicators: false) {
                        LazyVStack(alignment: .leading, spacing: 14) {
                            ForEach(Array(lyricsService.lines.enumerated()), id: \.element.id) { index, line in
                                Button(action: {
                                    // Click-to-Seek functionality!
                                    mediaService.seek(to: line.timestamp)
                                }) {
                                    HStack(spacing: 8) {
                                        Text(line.text)
                                            .font(.system(size: line.isActive ? 18 : 14, weight: line.isActive ? .bold : .medium, design: .rounded))
                                            .foregroundColor(line.isActive ? .white : .white.opacity(0.35))
                                            .shadow(color: line.isActive ? Color.pink.opacity(0.6) : .clear, radius: 8, x: 0, y: 0)
                                            .animation(.spring(response: 0.3, dampingFraction: 0.7), value: line.isActive)

                                        Spacer()

                                        if line.isActive {
                                            Circle()
                                                .fill(Color.pink)
                                                .frame(width: 6, height: 6)
                                        }
                                    }
                                    .padding(.horizontal, 16)
                                    .padding(.vertical, 4)
                                    .contentShape(Rectangle())
                                }
                                .buttonStyle(.plain)
                                .id(index)
                            }
                        }
                        .padding(.vertical, 12)
                    }
                    .onReceive(lyricsService.$activeIndex) { newIdx in
                        if newIdx >= 0 {
                            withAnimation(.spring(response: 0.45, dampingFraction: 0.8)) {
                                proxy.scrollTo(newIdx, anchor: .center)
                            }
                        }
                    }
                }
            }
        }
        .frame(maxHeight: .infinity)
    }
}
