import AppKit
import SwiftUI

/// Controller responsible for sizing, positioning, and anchoring the Dynamic Island on macOS
@MainActor
public final class WindowController: ObservableObject {
    public static let shared = WindowController()

    public private(set) var panel: FloatingPanel?

    @Published public var currentDisplayMode: IslandDisplayMode = .dynamicIsland
    @Published public var verticalOffset: CGFloat = 12.0

    private init() {}

    public func setupWindow() {
        guard let screen = NSScreen.main else { return }

        let s = SettingsManager.shared.settings
        self.currentDisplayMode = s.islandStyle
        self.verticalOffset = (s.islandStyle == .macBookNotch) ? 0 : CGFloat(s.topOffset)

        let initialWidth: CGFloat = 800
        let initialHeight: CGFloat = 450

        let xPos = screen.frame.midX - (initialWidth / 2.0)
        let yPos = screen.frame.maxY - initialHeight - (currentDisplayMode == .macBookNotch ? 0 : verticalOffset)

        let rect = NSRect(x: xPos, y: yPos, width: initialWidth, height: initialHeight)
        let panel = FloatingPanel(contentRect: rect)

        let rootView = IslandMainView()
            .environmentObject(self)
            .environmentObject(MediaService.shared)
            .environmentObject(LyricsService.shared)
            .environmentObject(HardwareControlService.shared)
            .environmentObject(SystemMonitorService.shared)
            .environmentObject(NotificationService.shared)
            .environmentObject(StartupService.shared)
            .environmentObject(SettingsManager.shared)

        panel.contentView = NSHostingView(rootView: rootView)
        panel.orderFrontRegardless()

        self.panel = panel

        // Check if device has a physical MacBook notch
        if let topInset = screen.safeAreaInsets.top as CGFloat?, topInset > 24.0, s.islandStyle == .macBookNotch {
            self.verticalOffset = 0
            reposition()
        }
    }

    public func applySettings(_ s: MacAppSettings) {
        self.currentDisplayMode = s.islandStyle
        self.verticalOffset = (s.islandStyle == .macBookNotch) ? 0 : CGFloat(s.topOffset)
        reposition()
    }

    public func toggleDisplayMode() {
        currentDisplayMode = (currentDisplayMode == .dynamicIsland) ? .macBookNotch : .dynamicIsland
        verticalOffset = (currentDisplayMode == .macBookNotch) ? 0 : CGFloat(SettingsManager.shared.settings.topOffset)
        var s = SettingsManager.shared.settings
        s.islandStyle = currentDisplayMode
        SettingsManager.shared.save(s)
        reposition()
    }

    public func reposition() {
        guard let panel = panel, let screen = NSScreen.main else { return }
        let currentFrame = panel.frame
        let newY = screen.frame.maxY - currentFrame.height - (currentDisplayMode == .macBookNotch ? 0 : verticalOffset)
        panel.setFrameOrigin(NSPoint(x: screen.frame.midX - (currentFrame.width / 2.0), y: newY))
    }
}
