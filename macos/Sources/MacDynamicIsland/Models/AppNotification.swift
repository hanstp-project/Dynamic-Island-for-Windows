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
}
