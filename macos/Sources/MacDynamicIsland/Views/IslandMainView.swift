import SwiftUI
import Combine

/// Main container for the Dynamic Island / MacBook Notch on macOS with full 8 subpages, hover expansion, and authentic notch curvature
public struct IslandMainView: View {
    @EnvironmentObject private var windowController: WindowController
    @EnvironmentObject private var mediaService: MediaService
    @EnvironmentObject private var notifService: NotificationService
    @EnvironmentObject private var lyricsService: LyricsService
    @ObservedObject private var settingsManager = SettingsManager.shared
    @ObservedObject private var toolsService = ToolsService.shared

    @State private var expansionState: IslandExpansionState = .compact
    @State private var activePage: ExpandedSubpage = .media
    @State private var isHovered: Bool = false

    public init() {}

    private var currentWidth: CGFloat {
        switch expansionState {
        case .compact:
            let base = CGFloat(settingsManager.settings.compactWidth)
            return (toolsService.isTimerRunning || toolsService.isStopwatchRunning) ? max(base, 230) : base
        case .notificationExpanded:
            return 400
        case .lyricsExpanded:
            return CGFloat(settingsManager.settings.expandedWidth)
        case .pageExpanded(let subpage):
            if subpage == .camera {
                return max(CGFloat(settingsManager.settings.expandedWidth), 400)
            }
            return CGFloat(settingsManager.settings.expandedWidth)
        }
    }

    private var currentHeight: CGFloat {
        switch expansionState {
        case .compact:
            return CGFloat(settingsManager.settings.compactHeight)
        case .notificationExpanded:
            return 82
        case .lyricsExpanded:
            return 340
        case .pageExpanded(let subpage):
            switch subpage {
            case .camera:
                return 320
            case .media:
                return 280
            case .dashboard:
                return 270
            case .system, .clock, .weather:
                return 260
            case .tools, .settings:
                return 280
            }
        }
    }

    public var body: some View {
        VStack(spacing: 0) {
            ZStack {
                // Background: Solid Jet-Black Matte (OLED pitch black, non-glossy)
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
                            // Top Header: Title & Switcher | Dots Navigation | Settings & Collapse (Identical to Windows Edition)
                            HStack(alignment: .center, spacing: 8) {
                                // Left: Subpage Title + Mode Switcher Button
                                HStack(spacing: 6) {
                                    Text(page.rawValue)
                                        .font(.system(size: 11, weight: .bold, design: .rounded))
                                        .foregroundColor(.white.opacity(0.85))

                                    Button(action: {
                                        withAnimation(.spring(response: 0.35, dampingFraction: 0.75)) {
                                            windowController.toggleDisplayMode()
                                        }
                                    }) {
                                        HStack(spacing: 3) {
                                            Image(systemName: windowController.currentDisplayMode == .macBookNotch ? "laptopcomputer" : "capsule.fill")
                                                .font(.system(size: 8.5))
                                            Text(windowController.currentDisplayMode == .macBookNotch ? "Notch" : "Island")
                                                .font(.system(size: 8.5, weight: .bold, design: .rounded))
                                        }
                                        .foregroundColor(Color(hex: "#0A84FF"))
                                        .padding(.horizontal, 6)
                                        .padding(.vertical, 2.5)
                                        .background(Color(hex: "#0A84FF").opacity(0.18))
                                        .clipShape(Capsule())
                                    }
                                    .buttonStyle(.plain)
                                    .help("Ganti Mode: Dynamic Island <-> MacBook Notch")
                                }

                                Spacer()

                                // Center: Page Indicator Dots (Interactive)
                                HStack(spacing: 5) {
                                    ForEach(ExpandedSubpage.allCases) { subpage in
                                        Button(action: {
                                            withAnimation(.spring(response: 0.32, dampingFraction: 0.75)) {
                                                activePage = subpage
                                                expansionState = .pageExpanded(subpage)
                                            }
                                        }) {
                                            Circle()
                                                .fill(activePage == subpage ? Color.white : Color.white.opacity(0.24))
                                                .frame(width: activePage == subpage ? 8 : 5, height: activePage == subpage ? 8 : 5)
                                                .shadow(color: activePage == subpage ? Color.white.opacity(0.6) : Color.clear, radius: 2)
                                        }
                                        .buttonStyle(.plain)
                                        .help(subpage.rawValue)
                                    }
                                }

                                Spacer()

                                // Right: Settings Window Button & Collapse Button
                                HStack(spacing: 6) {
                                    Button(action: {
                                        SettingsWindowController.shared.show()
                                    }) {
                                        Image(systemName: "gearshape.fill")
                                            .font(.system(size: 10.5, weight: .bold))
                                            .foregroundColor(.white.opacity(0.75))
                                            .frame(width: 22, height: 22)
                                            .background(Color.white.opacity(0.08))
                                            .clipShape(Circle())
                                    }
                                    .buttonStyle(.plain)
                                    .help("Buka Jendela Pengaturan (Settings)...")

                                    Button(action: {
                                        withAnimation(.spring(response: 0.36, dampingFraction: 0.72)) {
                                            expansionState = .compact
                                        }
                                    }) {
                                        Image(systemName: "chevron.up")
                                            .font(.system(size: 9.5, weight: .bold))
                                            .foregroundColor(.white.opacity(0.75))
                                            .frame(width: 22, height: 22)
                                            .background(Color.white.opacity(0.08))
                                            .clipShape(Circle())
                                    }
                                    .buttonStyle(.plain)
                                    .help("Kecilkan Pulau")
                                }
                            }
                            .padding(.horizontal, 14)
                            .padding(.top, 10)
                            .padding(.bottom, 4)

                            // Subpage Body Content
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
                                case .settings:
                                    SettingsView()
                                }
                            }
                            .frame(maxWidth: .infinity, maxHeight: .infinity)

                            Divider()
                                .background(Color.white.opacity(0.10))

                            // 8-Icon Master Bottom Quick Switcher
                            HStack(spacing: 3) {
                                ForEach(ExpandedSubpage.allCases) { subpage in
                                    Button(action: {
                                        withAnimation(.spring(response: 0.32, dampingFraction: 0.75)) {
                                            activePage = subpage
                                            expansionState = .pageExpanded(subpage)
                                        }
                                    }) {
                                        HStack(spacing: 4) {
                                            Image(systemName: subpage.icon)
                                                .font(.system(size: 10, weight: activePage == subpage ? .bold : .medium))
                                            if activePage == subpage {
                                                Text(subpage.rawValue)
                                                    .font(.system(size: 9.5, weight: .bold, design: .rounded))
                                            }
                                        }
                                        .foregroundColor(activePage == subpage ? .white : .white.opacity(0.40))
                                        .padding(.horizontal, activePage == subpage ? 8 : 6)
                                        .padding(.vertical, 4)
                                        .background(activePage == subpage ? Color.white.opacity(0.16) : Color.clear)
                                        .clipShape(Capsule())
                                    }
                                    .buttonStyle(.plain)
                                    .help(subpage.rawValue)
                                }
                            }
                            .padding(.horizontal, 8)
                            .padding(.vertical, 4)
                            .background(Color.black.opacity(0.40))
                        }
                        .transition(.opacity)
                    }
                }
            }
            .frame(width: currentWidth, height: currentHeight)
            .contentShape(Rectangle())
            .clipShape(IslandShape(isNotch: windowController.currentDisplayMode == .macBookNotch))
            .overlay(
                IslandShape(isNotch: windowController.currentDisplayMode == .macBookNotch)
                    .stroke(Color.white.opacity(0.10), lineWidth: 0.8)
            )
            .shadow(color: Color.black.opacity(0.55), radius: 18, x: 0, y: 8)
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
        // Pure Solid Jet-Black Matte (OLED pitch black, non-glossy, no white sheen/glare)
        Color.black.opacity(settingsManager.settings.pillOpacity)
    }

    // MARK: - Interactions
    private func handleTap() {
        guard settingsManager.settings.expandOnClick else { return }
        withAnimation(.spring(response: 0.36, dampingFraction: 0.72)) {
            if expansionState == .compact {
                let target = mediaService.isMediaAvailable ? .media : activePage
                expansionState = .pageExpanded(target)
            }
        }
    }

    private func handleHover(_ hovering: Bool) {
        guard settingsManager.settings.expandOnHover else { return }
        if hovering {
            if expansionState == .compact {
                withAnimation(.spring(response: 0.36, dampingFraction: 0.72)) {
                    let target = mediaService.isMediaAvailable ? .media : activePage
                    expansionState = .pageExpanded(target)
                }
            }
        } else {
            // Auto collapse to compact when mouse leaves, unless viewing lyrics, camera, tools, or settings
            if case .pageExpanded(let p) = expansionState {
                if p != .camera && p != .tools && p != .settings {
                    withAnimation(.spring(response: 0.36, dampingFraction: 0.72)) {
                        expansionState = .compact
                    }
                }
            }
        }
    }
}

/// Dynamic Island shape: either floating capsule or authentic MacBook notch with top concave curve-out ears
public struct IslandShape: Shape {
    public var isNotch: Bool

    public func path(in rect: CGRect) -> Path {
        if isNotch {
            var path = Path()
            let earRadius: CGFloat = 10
            let bottomRadius: CGFloat = min(16, rect.height / 2)

            // 1. Start at top-left ear (flush against top screen bezel)
            path.move(to: CGPoint(x: rect.minX, y: rect.minY))

            // 2. Left concave ear: curve inward from top bezel down to notch side
            path.addQuadCurve(
                to: CGPoint(x: rect.minX + earRadius, y: rect.minY + earRadius),
                control: CGPoint(x: rect.minX + earRadius, y: rect.minY)
            )

            // 3. Left vertical edge going down
            path.addLine(to: CGPoint(x: rect.minX + earRadius, y: rect.maxY - bottomRadius))

            // 4. Bottom-left convex rounded corner
            path.addQuadCurve(
                to: CGPoint(x: rect.minX + earRadius + bottomRadius, y: rect.maxY),
                control: CGPoint(x: rect.minX + earRadius, y: rect.maxY)
            )

            // 5. Bottom horizontal edge
            path.addLine(to: CGPoint(x: rect.maxX - earRadius - bottomRadius, y: rect.maxY))

            // 6. Bottom-right convex rounded corner
            path.addQuadCurve(
                to: CGPoint(x: rect.maxX - earRadius, y: rect.maxY - bottomRadius),
                control: CGPoint(x: rect.maxX - earRadius, y: rect.maxY)
            )

            // 7. Right vertical edge going up
            path.addLine(to: CGPoint(x: rect.maxX - earRadius, y: rect.minY + earRadius))

            // 8. Right concave ear: curve outward up into top bezel
            path.addQuadCurve(
                to: CGPoint(x: rect.maxX, y: rect.minY),
                control: CGPoint(x: rect.maxX - earRadius, y: rect.minY)
            )

            // 9. Close top edge
            path.closeSubpath()
            return path
        } else {
            return RoundedRectangle(cornerRadius: min(24, rect.height / 2), style: .continuous).path(in: rect)
        }
    }
}
