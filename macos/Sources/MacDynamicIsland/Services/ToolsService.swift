import Foundation
import Combine
import AppKit

@MainActor
public final class ToolsService: ObservableObject {
    public static let shared = ToolsService()

    // MARK: - Stopwatch
    @Published public var stopwatchElapsed: Double = 0.0
    @Published public var isStopwatchRunning: Bool = false
    @Published public var stopwatchLaps: [String] = []

    // MARK: - Countdown Timer
    @Published public var timerRemaining: Int = 300 // 5 minutes default
    @Published public var isTimerRunning: Bool = false
    @Published public var initialTimerSeconds: Int = 300

    // MARK: - Notes / Scratchpad
    @Published public var userNotes: String = "" {
        didSet {
            UserDefaults.standard.set(userNotes, forKey: "MacDynamicIsland_Notes")
        }
    }

    private var stopwatchTimer: Timer?
    private var countdownTimer: Timer?

    public init() {
        self.userNotes = UserDefaults.standard.string(forKey: "MacDynamicIsland_Notes") ?? "Catatan Cepat:\n- Meeting jam 14:00\n- Review PR Dynamic Island"
    }

    // MARK: - Stopwatch Controls
    public func toggleStopwatch() {
        if isStopwatchRunning {
            stopwatchTimer?.invalidate()
            stopwatchTimer = nil
            isStopwatchRunning = false
        } else {
            isStopwatchRunning = true
            stopwatchTimer = Timer.scheduledTimer(withTimeInterval: 0.05, repeats: true) { [weak self] _ in
                Task { @MainActor [weak self] in
                    self?.stopwatchElapsed += 0.05
                }
            }
        }
    }

    public func resetStopwatch() {
        stopwatchTimer?.invalidate()
        stopwatchTimer = nil
        isStopwatchRunning = false
        stopwatchElapsed = 0.0
        stopwatchLaps.removeAll()
    }

    public func recordLap() {
        let lapStr = formatStopwatch(stopwatchElapsed)
        stopwatchLaps.insert("Lap \(stopwatchLaps.count + 1): \(lapStr)", at: 0)
    }

    public func formatStopwatch(_ sec: Double) -> String {
        let totalCentis = Int(sec * 100)
        let mins = totalCentis / 6000
        let secs = (totalCentis % 6000) / 100
        let centis = totalCentis % 100
        return String(format: "%02d:%02d.%02d", mins, secs, centis)
    }

    // MARK: - Countdown Timer Controls
    public func startCountdown(seconds: Int) {
        countdownTimer?.invalidate()
        initialTimerSeconds = seconds
        timerRemaining = seconds
        isTimerRunning = true

        countdownTimer = Timer.scheduledTimer(withTimeInterval: 1.0, repeats: true) { [weak self] _ in
            Task { @MainActor [weak self] in
                guard let self = self else { return }
                if self.timerRemaining > 0 {
                    self.timerRemaining -= 1
                } else {
                    self.isTimerRunning = false
                    self.countdownTimer?.invalidate()
                    self.countdownTimer = nil
                    NSSound.beep()
                }
            }
        }
    }

    public func stopCountdown() {
        countdownTimer?.invalidate()
        countdownTimer = nil
        isTimerRunning = false
    }

    public func formatCountdown(_ sec: Int) -> String {
        let mins = sec / 60
        let s = sec % 60
        return String(format: "%02d:%02d", mins, s)
    }
}
