import SwiftUI

@main
struct MacDynamicIslandApp: App {
    @NSApplicationDelegateAdaptor(AppDelegate.self) var appDelegate

    var body: some Scene {
        // Settings scene or empty scene since the overlay is driven by custom NSPanel
        Settings {
            EmptyView()
        }
    }
}
