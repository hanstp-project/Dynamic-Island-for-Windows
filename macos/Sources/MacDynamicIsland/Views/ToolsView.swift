import SwiftUI

/// Tools subpage: Stopwatch, Countdown Timer, and Quick Notes Scratchpad
public struct ToolsView: View {
    @ObservedObject private var toolsService = ToolsService.shared
    @State private var selectedToolTab: Int = 0 // 0: Stopwatch, 1: Timer, 2: Notes

    public init() {}

    public var body: some View {
        VStack(spacing: 10) {
            // Segmented Picker Header
            HStack(spacing: 6) {
                ToolTabButton(title: "Stopwatch", icon: "stopwatch.fill", isSelected: selectedToolTab == 0) {
                    selectedToolTab = 0
                }
                ToolTabButton(title: "Timer", icon: "timer", isSelected: selectedToolTab == 1) {
                    selectedToolTab = 1
                }
                ToolTabButton(title: "Quick Notes", icon: "note.text", isSelected: selectedToolTab == 2) {
                    selectedToolTab = 2
                }
                Spacer()
            }

            Divider()
                .background(Color.white.opacity(0.1))

            // Body according to selected tool
            if selectedToolTab == 0 {
                // STOPWATCH
                HStack(spacing: 20) {
                    VStack(spacing: 8) {
                        Text(toolsService.formatStopwatch(toolsService.stopwatchElapsed))
                            .font(.system(size: 26, weight: .bold, design: .monospaced))
                            .foregroundColor(.white)

                        HStack(spacing: 12) {
                            Button(action: { toolsService.toggleStopwatch() }) {
                                HStack(spacing: 4) {
                                    Image(systemName: toolsService.isStopwatchRunning ? "pause.fill" : "play.fill")
                                    Text(toolsService.isStopwatchRunning ? "Jeda" : "Mulai")
                                }
                                .font(.system(size: 11, weight: .bold))
                                .foregroundColor(toolsService.isStopwatchRunning ? .orange : .green)
                                .padding(.horizontal, 12)
                                .padding(.vertical, 5)
                                .background(Color.white.opacity(0.12))
                                .clipShape(Capsule())
                            }
                            .buttonStyle(.plain)

                            Button(action: { toolsService.recordLap() }) {
                                Text("Lap")
                                    .font(.system(size: 11, weight: .semibold))
                                    .foregroundColor(.white.opacity(0.8))
                                    .padding(.horizontal, 10)
                                    .padding(.vertical, 5)
                                    .background(Color.white.opacity(0.08))
                                    .clipShape(Capsule())
                            }
                            .buttonStyle(.plain)
                            .disabled(!toolsService.isStopwatchRunning)

                            Button(action: { toolsService.resetStopwatch() }) {
                                Text("Reset")
                                    .font(.system(size: 11, weight: .semibold))
                                    .foregroundColor(.red.opacity(0.8))
                                    .padding(.horizontal, 10)
                                    .padding(.vertical, 5)
                                    .background(Color.white.opacity(0.08))
                                    .clipShape(Capsule())
                            }
                            .buttonStyle(.plain)
                        }
                    }
                    .frame(maxWidth: .infinity)

                    // Laps List
                    ScrollView(.vertical, showsIndicators: false) {
                        VStack(alignment: .leading, spacing: 4) {
                            if toolsService.stopwatchLaps.isEmpty {
                                Text("Belum ada catatan lap")
                                    .font(.system(size: 10))
                                    .foregroundColor(.white.opacity(0.3))
                            } else {
                                ForEach(toolsService.stopwatchLaps, id: \.self) { lap in
                                    Text(lap)
                                        .font(.system(size: 10, weight: .medium, design: .monospaced))
                                        .foregroundColor(.white.opacity(0.8))
                                }
                            }
                        }
                    }
                    .frame(width: 140, height: 75)
                    .padding(6)
                    .background(Color.white.opacity(0.04))
                    .cornerRadius(8)
                }
            } else if selectedToolTab == 1 {
                // COUNTDOWN TIMER
                HStack(spacing: 20) {
                    VStack(spacing: 6) {
                        Text(toolsService.formatCountdown(toolsService.timerRemaining))
                            .font(.system(size: 28, weight: .bold, design: .monospaced))
                            .foregroundColor(toolsService.timerRemaining <= 10 && toolsService.isTimerRunning ? .red : .white)

                        HStack(spacing: 12) {
                            Button(action: {
                                if toolsService.isTimerRunning {
                                    toolsService.stopCountdown()
                                } else {
                                    toolsService.startCountdown(seconds: toolsService.timerRemaining)
                                }
                            }) {
                                Text(toolsService.isTimerRunning ? "Stop" : "Mulai")
                                    .font(.system(size: 11, weight: .bold))
                                    .foregroundColor(toolsService.isTimerRunning ? .red : .green)
                                    .padding(.horizontal, 14)
                                    .padding(.vertical, 5)
                                    .background(Color.white.opacity(0.12))
                                    .clipShape(Capsule())
                            }
                            .buttonStyle(.plain)

                            Button(action: { toolsService.startCountdown(seconds: 300) }) {
                                Text("Reset 5m")
                                    .font(.system(size: 11, weight: .semibold))
                                    .foregroundColor(.white.opacity(0.7))
                                    .padding(.horizontal, 10)
                                    .padding(.vertical, 5)
                                    .background(Color.white.opacity(0.08))
                                    .clipShape(Capsule())
                            }
                            .buttonStyle(.plain)
                        }
                    }
                    .frame(maxWidth: .infinity)

                    // Quick presets
                    VStack(alignment: .leading, spacing: 4) {
                        Text("Preset Cepat:")
                            .font(.system(size: 10, weight: .semibold))
                            .foregroundColor(.white.opacity(0.5))

                        HStack(spacing: 6) {
                            PresetPill(title: "1m", sec: 60)
                            PresetPill(title: "5m", sec: 300)
                            PresetPill(title: "15m", sec: 900)
                            PresetPill(title: "25m 🍅", sec: 1500)
                        }
                    }
                }
            } else {
                // QUICK NOTES SCRATCHPAD
                VStack(alignment: .leading, spacing: 4) {
                    HStack {
                        Text("Catatan Cepat (Otomatis Tersimpan):")
                            .font(.system(size: 10, weight: .semibold))
                            .foregroundColor(.white.opacity(0.5))
                        Spacer()
                    }

                    TextEditor(text: $toolsService.userNotes)
                        .font(.system(size: 12, design: .rounded))
                        .background(Color.white.opacity(0.06))
                        .cornerRadius(8)
                        .foregroundColor(.white)
                        .frame(height: 75)
                }
            }
        }
        .padding(14)
    }
}

struct ToolTabButton: View {
    let title: String
    let icon: String
    let isSelected: Bool
    let action: () -> Void

    var body: some View {
        Button(action: action) {
            HStack(spacing: 5) {
                Image(systemName: icon)
                    .font(.system(size: 10))
                Text(title)
                    .font(.system(size: 11, weight: isSelected ? .bold : .medium, design: .rounded))
            }
            .foregroundColor(isSelected ? .white : .white.opacity(0.5))
            .padding(.horizontal, 10)
            .padding(.vertical, 4)
            .background(isSelected ? Color.blue.opacity(0.4) : Color.clear)
            .clipShape(Capsule())
        }
        .buttonStyle(.plain)
    }
}

struct PresetPill: View {
    let title: String
    let sec: Int

    var body: some View {
        Button(action: {
            ToolsService.shared.startCountdown(seconds: sec)
        }) {
            Text(title)
                .font(.system(size: 10, weight: .semibold))
                .foregroundColor(.white)
                .padding(.horizontal, 8)
                .padding(.vertical, 4)
                .background(Color.white.opacity(0.12))
                .clipShape(Capsule())
        }
        .buttonStyle(.plain)
    }
}
