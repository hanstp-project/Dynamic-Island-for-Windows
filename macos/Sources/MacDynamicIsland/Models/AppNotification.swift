import Foundation
import AppKit

public struct AppNotification: Identifiable, Equatable {
    public let id = UUID()
    public var appName: String
    public var sender: String
    public var message: String
    public var icon: NSImage?
    public var receivedAt: Date
    public var accentColorHex: String

    public init(
        appName: String,
        sender: String,
        message: String,
        icon: NSImage? = nil,
        receivedAt: Date = Date(),
        accentColorHex: String = "#25D366" // Default WhatsApp green
    ) {
        self.appName = appName
        self.sender = sender
        self.message = message
        self.icon = icon
        self.receivedAt = receivedAt
        self.accentColorHex = accentColorHex
    }

    public static func sampleWhatsApp() -> AppNotification {
        AppNotification(
            appName: "WhatsApp",
            sender: "Budi Santoso",
            message: "Halo! Aplikasi macOS Dynamic Island nya sudah jalan?",
            accentColorHex: "#25D366"
        )
    }

    public static func sampleTelegram() -> AppNotification {
        AppNotification(
            appName: "Telegram",
            sender: "Hanif Project",
            message: "Project Mac Dynamic Island update v1.0.0 siap di-test!",
            accentColorHex: "#229ED9"
        )
    }

    public static func sampleSpotify() -> AppNotification {
        AppNotification(
            appName: "Spotify",
            sender: "Now Playing",
            message: "Bohemian Rhapsody • Queen",
            accentColorHex: "#1DB954"
        )
    }

    public static func sampleBattery() -> AppNotification {
        AppNotification(
            appName: "System",
            sender: "Baterai Rendah",
            message: "Sisa baterai 15%. Harap sambungkan MacBook ke charger.",
            accentColorHex: "#FF3B30"
        )
    }
}
