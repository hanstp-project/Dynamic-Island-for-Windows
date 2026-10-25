import Foundation
import ServiceManagement

/// Handles "Start with macOS" registration via modern ServiceManagement API (macOS 13+)
@MainActor
public final class StartupService: ObservableObject {
    public static let shared = StartupService()

    @Published public private(set) var isEnabled: Bool = false

    public init() {
        checkStatus()
    }

    public func checkStatus() {
        if #available(macOS 13.0, *) {
            self.isEnabled = (SMAppService.mainApp.status == .enabled)
        }
    }

    public func setEnabled(_ enable: Bool) {
        if #available(macOS 13.0, *) {
            do {
                if enable {
                    try SMAppService.mainApp.register()
                } else {
                    try SMAppService.mainApp.unregister()
                }
                self.isEnabled = enable
            } catch {
                print("[StartupService] Failed to update login item: \(error.localizedDescription)")
            }
        }
    }
}
