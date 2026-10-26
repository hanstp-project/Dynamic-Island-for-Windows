import Foundation
import SwiftUI

public struct MacAppSettings: Codable, Equatable {
    public var islandStyle: IslandDisplayMode = .dynamicIsland
    public var theme: String = "Dark"
    public var compactWidth: Double = 136.0
    public var compactHeight: Double = 32.0
    public var expandedWidth: Double = 380.0
    public var topOffset: Double = 8.0
    public var pillOpacity: Double = 1.0

    // Features
    public var mediaEnabled: Bool = true
    public var lyricsEnabled: Bool = true
    public var cameraEnabled: Bool = true
    public var systemMonitorEnabled: Bool = true
    public var notificationsEnabled: Bool = true
    public var notificationTimeout: Int = 3

    // Behavior
    public var expandOnHover: Bool = false
    public var hoverDelayMs: Int = 350
    public var expandOnClick: Bool = true
    public var autoCollapseSeconds: Int = 15
    public var launchOnStartup: Bool = true

    public init() {}
}

@MainActor
public final class SettingsManager: ObservableObject {
    public static let shared = SettingsManager()

    @Published public var settings: MacAppSettings = MacAppSettings()

    private let defaultsKey = "MacDynamicIsland_AppSettings"

    private init() {
        load()
    }

    public func load() {
        if let data = UserDefaults.standard.data(forKey: defaultsKey),
           let decoded = try? JSONDecoder().decode(MacAppSettings.self, from: data) {
            self.settings = decoded
        }
    }

    public func save(_ newSettings: MacAppSettings) {
        self.settings = newSettings
        if let encoded = try? JSONEncoder().encode(newSettings) {
            UserDefaults.standard.set(encoded, forKey: defaultsKey)
        }
        WindowController.shared.applySettings(newSettings)
    }

    public func resetDefaults() {
        save(MacAppSettings())
    }
}
