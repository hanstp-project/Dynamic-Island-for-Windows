import Foundation
import AppKit

/// Service for controlling macOS hardware sliders (Screen Brightness & System Volume)
@MainActor
public final class HardwareControlService: ObservableObject {
    public static let shared = HardwareControlService()

    @Published public var brightness: Double = 80.0
    @Published public var volume: Double = 50.0

    public init() {
        refreshValues()
    }

    public func refreshValues() {
        fetchSystemVolume()
        fetchBrightness()
    }

    // MARK: - Volume Control
    public func setVolume(_ newVol: Double) {
        let clamped = min(max(newVol, 0), 100)
        self.volume = clamped
        let script = "set volume output volume \(Int(clamped))"
        _ = runAppleScript(script)
    }

    private func fetchSystemVolume() {
        let script = "output volume of (get volume settings)"
        if let output = runAppleScript(script), let val = Double(output.trimmingCharacters(in: .whitespacesAndNewlines)) {
            self.volume = val
        }
    }

    // MARK: - Brightness Control
    public func setBrightness(_ newBright: Double) {
        let clamped = min(max(newBright, 0), 100)
        self.brightness = clamped

        // Set brightness via DisplayServices or CoreDisplay
        let normalized = Float(clamped / 100.0)
        DisplayServicesSetBrightness(CGMainDisplayID(), normalized)
    }

    private func fetchBrightness() {
        var current: Float = 0.8
        DisplayServicesGetBrightness(CGMainDisplayID(), &current)
        self.brightness = Double(current * 100.0)
    }

    private func runAppleScript(_ script: String) -> String? {
        var error: NSDictionary?
        if let scriptObject = NSAppleScript(source: script) {
            let output = scriptObject.executeAndReturnError(&error)
            if error == nil {
                return output.stringValue
            }
        }
        return nil
    }
}

// MARK: - Private DisplayServices C Bridge
@_silgen_name("DisplayServicesGetBrightness")
private func DisplayServicesGetBrightness(_ display: CGDirectDisplayID, _ brightness: UnsafeMutablePointer<Float>) -> Int32

@_silgen_name("DisplayServicesSetBrightness")
private func DisplayServicesSetBrightness(_ display: CGDirectDisplayID, _ brightness: Float) -> Int32
