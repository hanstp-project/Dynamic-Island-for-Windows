import SwiftUI

/// Compact pill view when the island is idling, playing music, or running a live timer/stopwatch
public struct CompactIslandView: View {
    @EnvironmentObject private var mediaService: MediaService
    @EnvironmentObject private var notifService: NotificationService
    @ObservedObject private var toolsService = ToolsService.shared

    public init() {}

    public var body: some View {
        HStack(spacing: 8) {
            // Left accessory (Timer, Stopwatch, Notification, or Album art)
            if toolsService.isTimerRunning {
                ZStack {
                    Circle()
                        .fill(Color.orange.opacity(0.2))
                        .frame(width: 22, height: 22)
                    Image(systemName: "timer")
                        .font(.system(size: 10, weight: .bold))
                        .foregroundColor(.orange)
                }
            } else if toolsService.isStopwatchRunning {
                ZStack {
                    Circle()
                        .fill(Color.green.opacity(0.2))
                        .frame(width: 22, height: 22)
                    Image(systemName: "stopwatch.fill")
                        .font(.system(size: 10, weight: .bold))
                        .foregroundColor(.green)
                }
            } else if let notif = notifService.currentNotification {
                Circle()
                    .fill(Color(hex: notif.accentColorHex))
                    .frame(width: 10, height: 10)
            } else if mediaService.isMediaAvailable {
                // Mini album thumbnail or music note
                ZStack {
                    RoundedRectangle(cornerRadius: 6)
                        .fill(Color.orange.opacity(0.85))
                        .frame(width: 22, height: 22)
                    Image(systemName: "music.note")
                        .font(.system(size: 11, weight: .bold))
                        .foregroundColor(.white)
                }
            } else {
                Circle()
                    .fill(Color.white.opacity(0.2))
                    .frame(width: 8, height: 8)
            }

            // Center text
            if toolsService.isTimerRunning {
                Text("Timer")
                    .font(.system(size: 12, weight: .semibold, design: .rounded))
                    .foregroundColor(.orange)
            } else if toolsService.isStopwatchRunning {
                Text("Stopwatch")
                    .font(.system(size: 12, weight: .semibold, design: .rounded))
                    .foregroundColor(.green)
            } else if let notif = notifService.currentNotification {
                Text("\(notif.appName): \(notif.sender)")
                    .font(.system(size: 12, weight: .semibold, design: .rounded))
                    .foregroundColor(.white)
                    .lineLimit(1)
            } else if mediaService.isMediaAvailable {
                Text(mediaService.currentTrack.title.isEmpty ? "Playing..." : mediaService.currentTrack.title)
                    .font(.system(size: 12, weight: .medium, design: .rounded))
                    .foregroundColor(.white)
                    .lineLimit(1)
            } else {
                Text("Dynamic Island")
                    .font(.system(size: 12, weight: .medium, design: .rounded))
                    .foregroundColor(.white.opacity(0.6))
            }

            Spacer(minLength: 4)

            // Right accessory (Live Timer countdown, Stopwatch elapsed, Audio visualizer, or Clock)
            if toolsService.isTimerRunning {
                Text(toolsService.formatCountdown(toolsService.timerRemaining))
                    .font(.system(size: 11, weight: .bold, design: .monospaced))
                    .foregroundColor(.orange)
            } else if toolsService.isStopwatchRunning {
                Text(toolsService.formatStopwatch(toolsService.stopwatchElapsed))
                    .font(.system(size: 11, weight: .bold, design: .monospaced))
                    .foregroundColor(.green)
            } else if mediaService.isMediaAvailable && mediaService.currentTrack.isPlaying {
                AnimatedAudioVisualizerView()
            } else {
                Text(Date(), style: .time)
                    .font(.system(size: 11, weight: .semibold, design: .monospaced))
                    .foregroundColor(.white.opacity(0.7))
            }
        }
        .padding(.horizontal, 12)
        .padding(.vertical, 6)
    }
}

/// Mini 3-bar animated sound visualizer
struct AnimatedAudioVisualizerView: View {
    @State private var animate = false

    var body: some View {
        HStack(spacing: 2) {
            RoundedRectangle(cornerRadius: 1)
                .fill(Color.green)
                .frame(width: 2.5, height: animate ? 14 : 5)
            RoundedRectangle(cornerRadius: 1)
                .fill(Color.green)
                .frame(width: 2.5, height: animate ? 7 : 16)
            RoundedRectangle(cornerRadius: 1)
                .fill(Color.green)
                .frame(width: 2.5, height: animate ? 15 : 8)
        }
        .animation(.easeInOut(duration: 0.35).repeatForever(autoreverses: true), value: animate)
        .onAppear { animate = true }
    }
}

extension Color {
    init(hex: String) {
        let hex = hex.trimmingCharacters(in: CharacterSet.alphanumerics.inverted)
        var int: UInt64 = 0
        Scanner(string: hex).scanHexInt64(&int)
        let a, r, g, b: UInt64
        switch hex.count {
        case 3:
            (a, r, g, b) = (255, (int >> 8) * 17, (int >> 4 & 0xF) * 17, (int & 0xF) * 17)
        case 6:
            (a, r, g, b) = (255, int >> 16, int >> 8 & 0xFF, int & 0xFF)
        case 8:
            (a, r, g, b) = (int >> 24, int >> 16 & 0xFF, int >> 8 & 0xFF, int & 0xFF)
        default:
            (a, r, g, b) = (255, 255, 255, 255)
        }
        self.init(
            .sRGB,
            red: Double(r) / 255,
            green: Double(g) / 255,
            blue: Double(b) / 255,
            opacity: Double(a) / 255
        )
    }
}
