import SwiftUI
import AppKit

/// Detailed System Health view with CPU, RAM breakdown, Battery, and Disk usage
public struct SystemHealthView: View {
    @EnvironmentObject private var monitorService: SystemMonitorService

    public init() {}

    private var diskUsage: (freeGB: Double, totalGB: Double) {
        if let attrs = try? FileManager.default.attributesOfFileSystem(forPath: NSHomeDirectory()) {
            let free = (attrs[.systemFreeSize] as? NSNumber)?.int64Value ?? 0
            let total = (attrs[.systemSize] as? NSNumber)?.int64Value ?? 0
            let freeGB = Double(free) / (1024 * 1024 * 1024)
            let totalGB = Double(total) / (1024 * 1024 * 1024)
            return (freeGB, totalGB)
        }
        return (128.0, 512.0)
    }

    public var body: some View {
        VStack(spacing: 12) {
            // Header
            HStack {
                HStack(spacing: 6) {
                    Image(systemName: "heart.text.square.fill")
                        .foregroundColor(.pink)
                    Text("System Health Monitor")
                        .font(.system(size: 13, weight: .bold, design: .rounded))
                        .foregroundColor(.white)
                }

                Spacer()

                Text("Apple Silicon / macOS")
                    .font(.system(size: 10, weight: .medium, design: .monospaced))
                    .foregroundColor(.white.opacity(0.4))
            }

            // Grid of 4 Health Panels
            HStack(spacing: 10) {
                // 1. CPU Load
                VStack(alignment: .leading, spacing: 6) {
                    HStack {
                        Image(systemName: "cpu")
                            .foregroundColor(.cyan)
                        Text("CPU Load")
                            .font(.system(size: 11, weight: .semibold))
                            .foregroundColor(.white.opacity(0.8))
                        Spacer()
                        Text(String(format: "%.0f%%", monitorService.cpuUsage))
                            .font(.system(size: 11, weight: .bold, design: .monospaced))
                            .foregroundColor(.cyan)
                    }

                    ProgressView(value: monitorService.cpuUsage, total: 100)
                        .tint(.cyan)

                    Text("Load Stabil • Apple M-Core")
                        .font(.system(size: 9))
                        .foregroundColor(.white.opacity(0.4))
                }
                .padding(10)
                .background(Color.white.opacity(0.06))
                .cornerRadius(10)

                // 2. RAM Memory
                VStack(alignment: .leading, spacing: 6) {
                    HStack {
                        Image(systemName: "memorychip")
                            .foregroundColor(.purple)
                        Text("RAM Usage")
                            .font(.system(size: 11, weight: .semibold))
                            .foregroundColor(.white.opacity(0.8))
                        Spacer()
                        Text(String(format: "%.0f%%", monitorService.ramUsage))
                            .font(.system(size: 11, weight: .bold, design: .monospaced))
                            .foregroundColor(.purple)
                    }

                    ProgressView(value: monitorService.ramUsage, total: 100)
                        .tint(.purple)

                    Text("Unified Memory Pool")
                        .font(.system(size: 9))
                        .foregroundColor(.white.opacity(0.4))
                }
                .padding(10)
                .background(Color.white.opacity(0.06))
                .cornerRadius(10)
            }

            HStack(spacing: 10) {
                // 3. Battery Health
                VStack(alignment: .leading, spacing: 6) {
                    HStack {
                        Image(systemName: monitorService.isCharging ? "bolt.batteryblock.fill" : "battery.75")
                            .foregroundColor(monitorService.isCharging ? .green : .orange)
                        Text("Baterai")
                            .font(.system(size: 11, weight: .semibold))
                            .foregroundColor(.white.opacity(0.8))
                        Spacer()
                        Text(String(format: "%.0f%%", monitorService.batteryLevel))
                            .font(.system(size: 11, weight: .bold, design: .monospaced))
                            .foregroundColor(monitorService.isCharging ? .green : .orange)
                    }

                    ProgressView(value: monitorService.batteryLevel, total: 100)
                        .tint(monitorService.isCharging ? .green : .orange)

                    Text(monitorService.isCharging ? "Charging (Adaptor Aktif)" : "Mode Baterai Laptop")
                        .font(.system(size: 9))
                        .foregroundColor(.white.opacity(0.4))
                }
                .padding(10)
                .background(Color.white.opacity(0.06))
                .cornerRadius(10)

                // 4. Storage / Disk
                VStack(alignment: .leading, spacing: 6) {
                    let (freeGB, totalGB) = diskUsage
                    let usedGB = max(totalGB - freeGB, 0)
                    let usedPct = totalGB > 0 ? (usedGB / totalGB) * 100 : 50

                    HStack {
                        Image(systemName: "internaldrive.fill")
                            .foregroundColor(.blue)
                        Text("Macintosh HD")
                            .font(.system(size: 11, weight: .semibold))
                            .foregroundColor(.white.opacity(0.8))
                        Spacer()
                        Text(String(format: "%.0f GB Free", freeGB))
                            .font(.system(size: 10, weight: .bold, design: .monospaced))
                            .foregroundColor(.blue)
                    }

                    ProgressView(value: usedPct, total: 100)
                        .tint(.blue)

                    Text(String(format: "%.0f GB dari %.0f GB Terpakai", usedGB, totalGB))
                        .font(.system(size: 9))
                        .foregroundColor(.white.opacity(0.4))
                }
                .padding(10)
                .background(Color.white.opacity(0.06))
                .cornerRadius(10)
            }
        }
        .padding(16)
    }
}
