import SwiftUI

/// Main container for the Dynamic Island / MacBook Notch on macOS
public struct IslandMainView: View {
    @EnvironmentObject private var windowController: WindowController
    @EnvironmentObject private var mediaService: MediaService
    @EnvironmentObject private var notifService: NotificationService
    @EnvironmentObject private var lyricsService: LyricsService

    @State private var expansionState: IslandExpansionState = .compact
    @State private var isHovered: Bool = false

    public init() {}

    private var currentWidth: CGFloat {
        switch expansionState {
        case .compact:
            return 220
        case .notificationExpanded:
            return 390
        case .mediaExpanded:
            return 430
        case .lyricsExpanded:
            return 450
        case .controlCenterExpanded:
            return 440
        case .toolsExpanded:
            return 360
        }
    }

    private var currentHeight: CGFloat {
        switch expansionState {
        case .compact:
            return 36
        case .notificationExpanded:
            return 82
        case .mediaExpanded:
            return 175
        case .lyricsExpanded:
            return 330
        case .controlCenterExpanded:
            return 195
        case .toolsExpanded:
            return 120
        }
    }

    public var body: some View {
        VStack(spacing: 0) {
            ZStack {
                // Background & Glassmorphic Pill
                islandBackground

                // Content View according to Expansion State
                Group {
                    switch expansionState {
                    case .compact:
                        CompactIslandView()
                            .transition(.opacity)

                    case .notificationExpanded:
                        NotificationBannerView(expansionState: $expansionState)
                            .transition(.opacity)

                    case .mediaExpanded:
                        ExpandedMediaPlayerView(expansionState: $expansionState)
                            .transition(.opacity)

                    case .lyricsExpanded:
                        LyricsKaraokeView(expansionState: $expansionState)
                            .transition(.opacity)

                    case .controlCenterExpanded:
                        ControlCenterDashboardView(expansionState: $expansionState)
                            .transition(.opacity)

                    case .toolsExpanded:
                        CompactIslandView()
                            .transition(.opacity)
                    }
                }
            }
            .frame(width: currentWidth, height: currentHeight)
            .clipShape(islandShape)
            .overlay(
                islandShape
                    .stroke(Color.white.opacity(0.14), lineWidth: 0.8)
            )
            .shadow(color: Color.black.opacity(0.45), radius: 18, x: 0, y: 8)
            // Spring animation for all size, shape, and state transitions
            .animation(.spring(response: 0.38, dampingFraction: 0.72, blendDuration: 0), value: expansionState)
            .animation(.spring(response: 0.38, dampingFraction: 0.72, blendDuration: 0), value: windowController.currentDisplayMode)
            .onHover { hovering in
                isHovered = hovering
                handleHover(hovering)
            }
            .onTapGesture {
                handleTap()
            }
            .onChange(of: notifService.isNotificationActive) { active in
                if active {
                    withAnimation(.spring(response: 0.36, dampingFraction: 0.72)) {
                        expansionState = .notificationExpanded
                    }
                } else if expansionState == .notificationExpanded {
                    withAnimation(.spring(response: 0.36, dampingFraction: 0.72)) {
                        expansionState = .compact
                    }
                }
            }

            Spacer()
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .top)
    }

    // MARK: - Shape & Background
    private var islandShape: some Shape {
        if windowController.currentDisplayMode == .macBookNotch {
            // Notch: flat top against bezel, rounded bottom corners
            return UnevenRoundedRectangle(
                topLeadingRadius: 0,
                bottomLeadingRadius: 20,
                bottomTrailingRadius: 20,
                topTrailingRadius: 0,
                style: .continuous
            )
        } else {
            // Island: full pill capsule
            return UnevenRoundedRectangle(
                cornerRadii: RectangleCornerRadii(
                    topLeading: 22,
                    bottomLeading: 22,
                    bottomTrailing: 22,
                    topTrailing: 22
                ),
                style: .continuous
            )
        }
    }

    private var islandBackground: some View {
        ZStack {
            // Jet-black OLED base
            Color.black.opacity(0.92)

            // Subtle top highlight
            LinearGradient(
                colors: [Color.white.opacity(0.08), Color.clear],
                startPoint: .top,
                endPoint: .center
            )
        }
    }

    // MARK: - Interactions
    private func handleTap() {
        withAnimation(.spring(response: 0.36, dampingFraction: 0.72)) {
            if expansionState == .compact {
                if mediaService.isMediaAvailable {
                    expansionState = .mediaExpanded
                } else {
                    expansionState = .controlCenterExpanded
                }
            } else {
                expansionState = .compact
            }
        }
    }

    private func handleHover(_ hovering: Bool) {
        if hovering {
            if expansionState == .compact {
                withAnimation(.spring(response: 0.36, dampingFraction: 0.72)) {
                    if mediaService.isMediaAvailable {
                        expansionState = .mediaExpanded
                    }
                }
            }
        } else {
            // Auto collapse to compact when mouse leaves, unless viewing lyrics
            if expansionState != .lyricsExpanded && expansionState != .compact {
                withAnimation(.spring(response: 0.36, dampingFraction: 0.72)) {
                    expansionState = .compact
                }
            }
        }
    }
}
