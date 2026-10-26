import AppKit
import SwiftUI

/// Window Controller managing the standalone Settings Window on macOS
@MainActor
public final class SettingsWindowController: NSObject, NSWindowDelegate {
    public static let shared = SettingsWindowController()

    private var window: NSWindow?

    public override init() {
        super.init()
    }

    public func show() {
        if let existing = window {
            existing.makeKeyAndOrderFront(nil)
            NSApp.activate(ignoringOtherApps: true)
            return
        }

        let win = NSWindow(
            contentRect: NSRect(x: 0, y: 0, width: 580, height: 700),
            styleMask: [.titled, .closable, .miniaturizable, .resizable],
            backing: .buffered,
            defer: false
        )

        win.title = "Mac Dynamic Island - Settings"
        win.titlebarAppearsTransparent = false
        win.backgroundColor = NSColor(red: 0.094, green: 0.094, blue: 0.106, alpha: 1.0)
        win.isReleasedWhenClosed = false
        win.delegate = self
        win.center()
        win.minSize = NSSize(width: 520, height: 500)

        let hostingView = NSHostingView(rootView: StandaloneSettingsView(onClose: { [weak self] in
            self?.close()
        }))
        win.contentView = hostingView

        self.window = win
        win.makeKeyAndOrderFront(nil)
        NSApp.activate(ignoringOtherApps: true)
    }

    public func close() {
        window?.close()
    }

    public func windowWillClose(_ notification: Notification) {
        window = nil
    }
}
