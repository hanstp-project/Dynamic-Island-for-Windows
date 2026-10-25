import AppKit
import SwiftUI

@MainActor
public final class AppDelegate: NSObject, NSApplicationDelegate {
    private var statusItem: NSStatusItem?

    public func applicationDidFinishLaunching(_ notification: Notification) {
        // Run as background accessory without Dock icon
        NSApp.setActivationPolicy(.accessory)

        // Setup Floating Island Window
        WindowController.shared.setupWindow()

        // Setup Menu Bar Status Icon
        setupMenuBar()
    }

    private func setupMenuBar() {
        statusItem = NSStatusBar.system.statusItem(withLength: NSStatusItem.variableLength)

        if let button = statusItem?.button {
            button.image = NSImage(systemSymbolName: "capsule.fill", accessibilityDescription: "Dynamic Island")
        }

        let menu = NSMenu()

        let titleItem = NSMenuItem(title: "🏝️ Mac Dynamic Island", action: nil, keyEquivalent: "")
        titleItem.isEnabled = false
        menu.addItem(titleItem)

        menu.addItem(NSMenuItem.separator())

        let toggleModeItem = NSMenuItem(title: "Toggle Island / Notch Mode", action: #selector(toggleMode), keyEquivalent: "m")
        toggleModeItem.target = self
        menu.addItem(toggleModeItem)

        let testNotifItem = NSMenuItem(title: "Test Notifikasi WhatsApp", action: #selector(testNotification), keyEquivalent: "t")
        testNotifItem.target = self
        menu.addItem(testNotifItem)

        menu.addItem(NSMenuItem.separator())

        let startupItem = NSMenuItem(title: "Start on macOS Login", action: #selector(toggleStartup), keyEquivalent: "")
        startupItem.target = self
        startupItem.state = StartupService.shared.isEnabled ? .on : .off
        menu.addItem(startupItem)

        menu.addItem(NSMenuItem.separator())

        let quitItem = NSMenuItem(title: "Keluar (Quit)", action: #selector(quitApp), keyEquivalent: "q")
        quitItem.target = self
        menu.addItem(quitItem)

        statusItem?.menu = menu
    }

    @objc private func toggleMode() {
        WindowController.shared.toggleDisplayMode()
    }

    @objc private func testNotification() {
        NotificationService.shared.triggerTestWhatsApp()
    }

    @objc private func toggleStartup(_ sender: NSMenuItem) {
        let newState = !StartupService.shared.isEnabled
        StartupService.shared.setEnabled(newState)
        sender.state = newState ? .on : .off
    }

    @objc private func quitApp() {
        NSApplication.shared.terminate(nil)
    }
}
