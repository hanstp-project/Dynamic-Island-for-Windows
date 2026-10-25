import SwiftUI

/// Control center dashboard with hardware sliders (Brightness & Volume), system monitor, and mode toggle
public struct ControlCenterDashboardView: View {
    @EnvironmentObject private var windowController: WindowController
    @EnvironmentObject private var hardwareService: HardwareControlService
    @EnvironmentObject private var monitorService: SystemMonitorService

    @Binding var expansionState: IslandExpansionState

    public init(expansionState: Binding<IslandExpansionState>) {
        self._expansionState = expansionState
    }

    public var body: some View {
        VStack(spacing: 14) {
            // Header: Title and Mode Switcher
            HStack {
                HStack(spacing: 6) {
                    Image(systemName: "slider.horizontal.3")
                        .font(.system(size: 13, weight: .bold))
                        .foregroundColor(.blue)
                    Text("Control Center")
                        .font(.system(size: 13, weight: .bold, design: .rounded))
                        .foregroundColor(.white)
                }

                Spacer()

                // Display Mode Toggle Button
                Button(action: {
                    withAnimation(.spring(response: 0.35, dampingFraction: 0.75)) {
                        windowController.toggleDisplayMode()
                    }
                }) {
                    HStack(spacing: 5) {
                        Image(systemName: windowController.currentDisplayMode == .macBookNotch ? "laptopcomputer" : "capsule.fill")
                            .font(.system(size: 11))
                        Text(windowController.currentDisplayMode.rawValue)
                            .font(.system(size: 11, weight: .semibold, design: .rounded))
                    }
                    .foregroundColor(.white)
                    .padding(.horizontal, 10)
                    .padding(.vertical, 5)
                    .background(Color.blue.opacity(0.35))
                    .clipShape(Capsule())
                }
                .buttonStyle(.plain)
                .help("Ganti Mode Tampilan (Island / Notch)")
            }

            // Sliders Section: Brightness and Volume
            HStack(spacing: 12) {
                // Brightness Slider
                VStack(alignment: .leading, spacing: 6) {
                    HStack {
                        Image(systemName: "sun.max.fill")
                            .foregroundColor(.yellow)
                            .font(.system(size: 12))
                        Text("Brightness")
                            .font(.system(size: 11, weight: .medium, design: .rounded))
                            .foregroundColor(.white.opacity(0.7))
                        Spacer()
                        Text("\(Int(hardwareService.brightness))%")
                            .font(.system(size: 11, weight: .semibold, design: .monospaced))
                            .foregroundColor(.white.opacity(0.8))
                    }

                    Slider(value: $hardwareService.brightness, in: 0...100) { _ in
                        hardwareService.setBrightness(hardwareService.brightness)
                    }
                    .accentColor(.yellow)
                }
                .padding(10)
                .background(Color.white.opacity(0.08))
                .cornerRadius(10)

                // Volume Slider
                VStack(alignment: .leading, spacing: 6) {
                    HStack {
                        Image(systemName: "speaker.wave.3.fill")
                            .foregroundColor(.blue)
                            .font(.system(size: 12))
                        Text("Volume")
                            .font(.system(size: 11, weight: .medium, design: .rounded))
                            .foregroundColor(.white.opacity(0.7))
                        Spacer()
                        Text("\(Int(hardwareService.volume))%")
                            .font(.system(size: 11, weight: .semibold, design: .monospaced))
                            .foregroundColor(.white.opacity(0.8))
                    }

                    Slider(value: $hardwareService.volume, in: 0...100) { _ in
                        hardwareService.setVolume(hardwareService.volume)
                    }
                    .accentColor(.blue)
                }
                .padding(10)
                .background(Color.white.opacity(0.08))
                .cornerRadius(10)
            }

            // System Monitor Stats: CPU, RAM, Battery
            HStack(spacing: 10) {
                // CPU
                StatCard(
                    icon: "cpu",
                    title: "CPU",
                    value: String(format: "%.0f%%", monitorService.cpuUsage),
                    color: .cyan
                )

                // RAM
                StatCard(
                    icon: "memorychip",
                    title: "RAM",
                    value: String(format: "%.0f%%", monitorService.ramUsage),
                    color: .purple
                )

                // Battery
                StatCard(
                    icon: monitorService.isCharging ? "bolt.batteryblock.fill" : "battery.75",
                    title: "Battery",
                    value: String(format: "%.0f%%", monitorService.batteryLevel),
                    color: monitorService.isCharging ? .green : .orange
                )

                // Network
                StatCard(
                    icon: "arrow.down.circle.fill",
                    title: "Net",
                    value: String(format: "%.0f KB/s", monitorService.downloadSpeedKB),
                    color: .blue
                )
            }
        }
        .padding(16)
    }
}

struct StatCard: View {
    let icon: String
    let title: String
    let value: String
    let color: Color

    var body: some View {
        VStack(spacing: 4) {
            Image(systemName: icon)
                .font(.system(size: 14))
                .foregroundColor(color)

            Text(value)
                .font(.system(size: 12, weight: .bold, design: .rounded))
                .foregroundColor(.white)

            Text(title)
                .font(.system(size: 9, weight: .medium))
                .foregroundColor(.white.opacity(0.5))
        }
        .frame(maxWidth: .infinity)
        .padding(.vertical, 8)
        .background(Color.white.opacity(0.06))
        .cornerRadius(10)
    }
}
