import Foundation

/// Defines how the island is visually mounted on macOS
public enum IslandDisplayMode: String, Codable, CaseIterable {
    /// Floating pill below top edge / camera
    case dynamicIsland = "Dynamic Island"
    /// Flush against top screen bezel or physical MacBook camera notch
    case macBookNotch = "MacBook Notch"
}

/// Defines current interactive expansion state of the island
public enum IslandExpansionState: Equatable {
    case compact
    case mediaExpanded
    case lyricsExpanded
    case notificationExpanded
    case controlCenterExpanded
    case toolsExpanded
}
