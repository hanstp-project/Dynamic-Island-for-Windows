import Foundation

/// Defines how the island is visually mounted on macOS
public enum IslandDisplayMode: String, Codable, CaseIterable {
    /// Floating pill below top edge / camera
    case dynamicIsland = "Dynamic Island"
    /// Flush against top screen bezel or physical MacBook camera notch
    case macBookNotch = "MacBook Notch"
}

/// The 7 distinct expanded subpages matching Windows edition
public enum ExpandedSubpage: String, CaseIterable, Identifiable {
    case media = "Music"
    case camera = "Retro Cam"
    case dashboard = "Dashboard"
    case system = "System Health"
    case clock = "Time & Date"
    case tools = "Tools"
    case weather = "Weather"

    public var id: String { rawValue }

    public var icon: String {
        switch self {
        case .media: return "music.note"
        case .camera: return "camera.aperture"
        case .dashboard: return "slider.horizontal.3"
        case .system: return "heart.text.square.fill"
        case .clock: return "clock.fill"
        case .tools: return "stopwatch.fill"
        case .weather: return "cloud.sun.fill"
        }
    }
}

/// Defines current interactive expansion state of the island
public enum IslandExpansionState: Equatable {
    case compact
    case notificationExpanded
    case pageExpanded(ExpandedSubpage)
    case lyricsExpanded
}
