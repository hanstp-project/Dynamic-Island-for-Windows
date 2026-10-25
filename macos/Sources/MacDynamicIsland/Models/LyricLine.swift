import Foundation

public struct LyricLine: Identifiable, Equatable {
    public let id = UUID()
    public let timestamp: Double   // Seconds from song start
    public let text: String
    public var isActive: Bool

    public init(timestamp: Double, text: String, isActive: Bool = false) {
        self.timestamp = timestamp
        self.text = text
        self.isActive = isActive
    }

    public var formattedTime: String {
        let s = Int(timestamp)
        let mins = s / 60
        let secs = s % 60
        return String(format: "%02d:%02d", mins, secs)
    }
}
