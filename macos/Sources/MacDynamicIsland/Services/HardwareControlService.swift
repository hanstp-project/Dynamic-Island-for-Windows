import Foundation
import AppKit

/// Service for controlling macOS hardware sliders (Screen Brightness & System Volume)
@MainActor
public final class HardwareControlService: ObservableObject {
    public static let shared = HardwareControlService()

    @Published public var brightness: Double = 80.0
    @Published public var volume: Double = 50.0

    private typealias GetBrightnessFunc = @convention(c) (CGDirectDisplayID, UnsafeMutablePointer<Float>) -> Int32
    private typealias SetBrightnessFunc = @convention(c) (CGDirectDisplayID, Float) -> Int32

    private var getBrightnessFn: GetBrightnessFunc?
    private var setBrightnessFn: SetBrightnessFunc?

    public init() {
        loadDisplayServices()
        refreshValues()
    }

    private func loadDisplayServices() {
        if let handle = dlopen("/System/Library/PrivateFrameworks/DisplayServices.framework/DisplayServices", RTLD_LAZY) {
            if let getSym = dlsym(handle, "DisplayServicesGetBrightness") {
                getBrightnessFn = unsafeBitCast(getSym, to: GetBrightnessFunc.self)
            }
            if let setSym = dlsym(handle, "DisplayServicesSetBrightness") {
                setBrightnessFn = unsafeBitCast(setSym, to: SetBrightnessFunc.self)
            }
        }
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
        let normalized = Float(clamped / 100.0)

        if let setFn = setBrightnessFn {
            _ = setFn(CGMainDisplayID(), normalized)
        }
    }

    private func fetchBrightness() {
        var current: Float = 0.8
        if let getFn = getBrightnessFn {
            _ = getFn(CGMainDisplayID(), &current)
            self.brightness = Double(current * 100.0)
        }
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
