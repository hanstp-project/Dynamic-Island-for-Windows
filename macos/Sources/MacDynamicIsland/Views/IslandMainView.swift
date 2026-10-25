import SwiftUI
import Combine

/// Custom shape for island (pill) or notch (flat top, rounded bottom)
public struct IslandShape: Shape {
    public var isNotch: Bool

    public var animatableData: Double {
        get { isNotch ? 1.0 : 0.0 }
        set { isNotch = newValue > 0.5 }
    }

    public func path(in rect: CGRect) -> Path {
        let radius: CGFloat = 20.0
        var path = Path()
        if isNotch {
            // Flat top against screen bezel, rounded bottom corners
            path.move(to: CGPoint(x: rect.minX, y: rect.minY))
            path.addLine(to: CGPoint(x: rect.maxX, y: rect.minY))
            path.addLine(to: CGPoint(x: rect.maxX, y: rect.maxY - radius))
            path.addArc(
                center: CGPoint(x: rect.maxX - radius, y: rect.maxY - radius),
                radius: radius,
                startAngle: .degrees(0),
                endAngle: .degrees(90),
                clockwise: false
            )
            path.addLine(to: CGPoint(x: rect.minX + radius, y: rect.maxY))
            path.addArc(
                center: CGPoint(x: rect.minX + radius, y: rect.maxY - radius),
                radius: radius,
                startAngle: .degrees(90),
                endAngle: .degrees(180),
                clockwise: false
            )
            path.closeSubpath()
        } else {
            // Full pill capsule
            path.addRoundedRect(in: rect, cornerSize: CGSize(width: 22, height: 22), style: .continuous)
        }
        return path
    }
}

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
            .clipShape(IslandShape(isNotch: windowController.currentDisplayMode == .macBookNotch))
            .overlay(
                IslandShape(isNotch: windowController.currentDisplayMode == .macBookNotch)
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
            .onReceive(notifService.$isNotificationActive) { active in
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
