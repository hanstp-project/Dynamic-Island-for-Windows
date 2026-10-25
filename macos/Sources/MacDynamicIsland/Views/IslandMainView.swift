import SwiftUI
import Combine

/// Main container for the Dynamic Island / MacBook Notch on macOS with full 7 subpages
public struct IslandMainView: View {
    @EnvironmentObject private var windowController: WindowController
    @EnvironmentObject private var mediaService: MediaService
    @EnvironmentObject private var notifService: NotificationService
    @EnvironmentObject private var lyricsService: LyricsService

    @State private var expansionState: IslandExpansionState = .compact
    @State private var activePage: ExpandedSubpage = .media
    @State private var isHovered: Bool = false

    public init() {}

    private var currentWidth: CGFloat {
        switch expansionState {
        case .compact:
            return 220
        case .notificationExpanded:
            return 390
        case .lyricsExpanded:
            return 460
        case .pageExpanded:
            return 460
        }
    }

    private var currentHeight: CGFloat {
        switch expansionState {
        case .compact:
            return 36
        case .notificationExpanded:
            return 82
        case .lyricsExpanded:
            return 330
        case .pageExpanded:
            return 245
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

                    case .lyricsExpanded:
                        LyricsKaraokeView(expansionState: $expansionState)
                            .transition(.opacity)

                    case .pageExpanded(let page):
                        VStack(spacing: 0) {
                            // Subpage Body
                            Group {
                                switch page {
                                case .media:
                                    ExpandedMediaPlayerView(expansionState: $expansionState)
                                case .camera:
                                    RetroCameraView()
                                case .dashboard:
                                    ControlCenterDashboardView(expansionState: $expansionState)
                                case .system:
                                    SystemHealthView()
                                case .clock:
                                    TimeDateView()
                                case .tools:
                                    ToolsView()
                                case .weather:
                                    WeatherView()
                                }
                            }
                            .frame(maxWidth: .infinity, maxHeight: .infinity)

                            Divider()
                                .background(Color.white.opacity(0.12))

                            // 7-Icon Master Navigation Bar
                            HStack(spacing: 4) {
                                ForEach(ExpandedSubpage.allCases) { subpage in
                                    Button(action: {
                                        withAnimation(.spring(response: 0.32, dampingFraction: 0.75)) {
                                            activePage = subpage
                                            expansionState = .pageExpanded(subpage)
                                        }
                                    }) {
                                        HStack(spacing: 4) {
                                            Image(systemName: subpage.icon)
                                                .font(.system(size: 11, weight: activePage == subpage ? .bold : .medium))
                                            if activePage == subpage {
                                                Text(subpage.rawValue)
                                                    .font(.system(size: 10, weight: .bold, design: .rounded))
                                            }
                                        }
                                        .foregroundColor(activePage == subpage ? .white : .white.opacity(0.45))
                                        .padding(.horizontal, activePage == subpage ? 10 : 8)
                                        .padding(.vertical, 5)
                                        .background(activePage == subpage ? Color.white.opacity(0.18) : Color.clear)
                                        .clipShape(Capsule())
                                    }
                                    .buttonStyle(.plain)
                                    .help(subpage.rawValue)
                                }

                                Spacer()

                                // Collapse Button
                                Button(action: {
                                    withAnimation(.spring(response: 0.36, dampingFraction: 0.72)) {
                                        expansionState = .compact
                                    }
                                }) {
                                    Image(systemName: "chevron.up")
                                        .font(.system(size: 11, weight: .bold))
                                        .foregroundColor(.white.opacity(0.4))
                                        .padding(6)
                                        .background(Color.white.opacity(0.08))
                                        .clipShape(Circle())
                                }
                                .buttonStyle(.plain)
                                .help("Kecilkan Pulau")
                            }
                            .padding(.horizontal, 12)
                            .padding(.vertical, 6)
                            .background(Color.black.opacity(0.3))
                        }
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
                    activePage = .media
                    expansionState = .pageExpanded(.media)
                } else {
                    activePage = .dashboard
                    expansionState = .pageExpanded(.dashboard)
                }
            }
        }
    }

    private func handleHover(_ hovering: Bool) {
        if hovering {
            if expansionState == .compact && mediaService.isMediaAvailable {
                withAnimation(.spring(response: 0.36, dampingFraction: 0.72)) {
                    activePage = .media
                    expansionState = .pageExpanded(.media)
                }
            }
        } else {
            // Auto collapse to compact when mouse leaves, unless viewing lyrics, camera, or tools
            if case .pageExpanded(let p) = expansionState {
                if p != .camera && p != .tools {
                    withAnimation(.spring(response: 0.36, dampingFraction: 0.72)) {
                        expansionState = .compact
                    }
                }
            }
        }
    }
}
