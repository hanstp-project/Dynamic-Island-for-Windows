import SwiftUI
import AppKit

/// Standalone Settings Window View for Mac Dynamic Island matching the Windows Settings design
public struct StandaloneSettingsView: View {
    @ObservedObject private var settingsManager = SettingsManager.shared
    @ObservedObject private var startupService = StartupService.shared

    @State private var draft: MacAppSettings = MacAppSettings()
    private var onClose: (() -> Void)?

    public init(onClose: (() -> Void)? = nil) {
        self.onClose = onClose
    }

    public var body: some View {
        VStack(spacing: 0) {
            // Header Bar
            VStack(alignment: .leading, spacing: 3) {
                Text("Dynamic Island for macOS")
                    .font(.system(size: 20, weight: .bold, design: .rounded))
                    .foregroundColor(.white)

                Text("Configure layout, behavior, and enabled features")
                    .font(.system(size: 12, weight: .regular))
                    .foregroundColor(Color(hex: "#8E8E93"))
            }
            .frame(maxWidth: .infinity, alignment: .leading)
            .padding(.horizontal, 24)
            .padding(.top, 20)
            .padding(.bottom, 14)

            Divider()
                .background(Color(hex: "#2C2C2E"))

            // Scrollable Content
            ScrollView(.vertical, showsIndicators: true) {
                VStack(alignment: .leading, spacing: 18) {
                    
                    // SECTION 1: APPEARANCE
                    VStack(alignment: .leading, spacing: 6) {
                        Text("APPEARANCE")
                            .font(.system(size: 11, weight: .semibold))
                            .foregroundColor(Color(hex: "#99EBEBF5"))
                            .padding(.leading, 4)

                        VStack(spacing: 16) {
                            // Island Style Dropdown
                            HStack {
                                VStack(alignment: .leading, spacing: 2) {
                                    Text("Island Style")
                                        .font(.system(size: 13, weight: .medium))
                                        .foregroundColor(.white)
                                    Text("Dynamic Island (Melayang) atau Notch (Menempel Bezel Atas)")
                                        .font(.system(size: 11))
                                        .foregroundColor(Color(hex: "#8E8E93"))
                                }
                                Spacer()
                                Picker("", selection: $draft.islandStyle) {
                                    Text("Notch Island (Bezel Atas)").tag(IslandDisplayMode.macBookNotch)
                                    Text("Dynamic Island (Melayang)").tag(IslandDisplayMode.dynamicIsland)
                                }
                                .pickerStyle(.menu)
                                .frame(width: 220)
                            }

                            Divider().background(Color(hex: "#333338"))

                            // Theme Dropdown
                            HStack {
                                VStack(alignment: .leading, spacing: 2) {
                                    Text("Theme")
                                        .font(.system(size: 13, weight: .medium))
                                        .foregroundColor(.white)
                                    Text("Color palette for island surfaces")
                                        .font(.system(size: 11))
                                        .foregroundColor(Color(hex: "#8E8E93"))
                                }
                                Spacer()
                                Picker("", selection: $draft.theme) {
                                    Text("Dark").tag("Dark")
                                    Text("Light").tag("Light")
                                }
                                .pickerStyle(.menu)
                                .frame(width: 140)
                            }

                            Divider().background(Color(hex: "#333338"))

                            // Compact Width
                            SliderSettingRow(
                                title: "Compact Width",
                                subtitle: "Default width in idle pill state",
                                value: $draft.compactWidth,
                                range: 90...220,
                                step: 1,
                                format: "%.0f px"
                            )

                            Divider().background(Color(hex: "#333338"))

                            // Compact Height
                            SliderSettingRow(
                                title: "Compact Height",
                                subtitle: "Pill height in idle state",
                                value: $draft.compactHeight,
                                range: 26...44,
                                step: 1,
                                format: "%.0f px"
                            )

                            Divider().background(Color(hex: "#333338"))

                            // Expanded Width
                            SliderSettingRow(
                                title: "Expanded Width",
                                subtitle: "Width when island is expanded",
                                value: $draft.expandedWidth,
                                range: 320...520,
                                step: 1,
                                format: "%.0f px"
                            )

                            Divider().background(Color(hex: "#333338"))

                            // Top Offset
                            SliderSettingRow(
                                title: "Top Offset",
                                subtitle: "Distance from the top of the monitor",
                                value: $draft.topOffset,
                                range: 0...80,
                                step: 1,
                                format: "%.0f px"
                            )

                            Divider().background(Color(hex: "#333338"))

                            // Pill Opacity
                            SliderSettingRow(
                                title: "Pill Opacity",
                                subtitle: "Surface background transparency",
                                value: $draft.pillOpacity,
                                range: 0.60...1.0,
                                step: 0.01,
                                format: "%.0f %%",
                                multiplier: 100
                            )
                        }
                        .padding(16)
                        .background(Color(hex: "#242428"))
                        .cornerRadius(12)
                        .overlay(RoundedRectangle(cornerRadius: 12).stroke(Color(hex: "#333338"), lineWidth: 1))
                    }

                    // SECTION 2: INTEGRATED FEATURES
                    VStack(alignment: .leading, spacing: 6) {
                        Text("INTEGRATED FEATURES")
                            .font(.system(size: 11, weight: .semibold))
                            .foregroundColor(Color(hex: "#99EBEBF5"))
                            .padding(.leading, 4)

                        VStack(spacing: 14) {
                            ToggleSettingRow(
                                title: "Media Player Integration",
                                subtitle: "Display current track and controls from Spotify, Apple Music, etc.",
                                isOn: $draft.mediaEnabled
                            )

                            Divider().background(Color(hex: "#333338"))

                            ToggleSettingRow(
                                title: "Synchronized Karaoke Lyrics",
                                subtitle: "LRCLIB word-by-word synced lyrics streaming with click-to-seek",
                                isOn: $draft.lyricsEnabled
                            )

                            Divider().background(Color(hex: "#333338"))

                            ToggleSettingRow(
                                title: "Retro Camera & Instant Polaroid",
                                subtitle: "Live webcam viewfinder with vintage filters and printable polaroids",
                                isOn: $draft.cameraEnabled
                            )

                            Divider().background(Color(hex: "#333338"))

                            ToggleSettingRow(
                                title: "System Health Monitoring",
                                subtitle: "Real-time CPU load, Unified RAM usage, and battery indicators",
                                isOn: $draft.systemMonitorEnabled
                            )

                            Divider().background(Color(hex: "#333338"))

                            ToggleSettingRow(
                                title: "Notifications Mirroring",
                                subtitle: "Display incoming macOS system and messaging alerts in the island",
                                isOn: $draft.notificationsEnabled
                            )

                            Divider().background(Color(hex: "#333338"))

                            SliderSettingRow(
                                title: "Notification Timeout",
                                subtitle: "Duration before notification auto-collapses back to island",
                                value: Binding(
                                    get: { Double(draft.notificationTimeout) },
                                    set: { draft.notificationTimeout = Int($0) }
                                ),
                                range: 1...15,
                                step: 1,
                                format: "%.0f s"
                            )
                        }
                        .padding(16)
                        .background(Color(hex: "#242428"))
                        .cornerRadius(12)
                        .overlay(RoundedRectangle(cornerRadius: 12).stroke(Color(hex: "#333338"), lineWidth: 1))
                    }

                    // SECTION 3: BEHAVIOR & STARTUP
                    VStack(alignment: .leading, spacing: 6) {
                        Text("BEHAVIOR & LAUNCH")
                            .font(.system(size: 11, weight: .semibold))
                            .foregroundColor(Color(hex: "#99EBEBF5"))
                            .padding(.leading, 4)

                        VStack(spacing: 14) {
                            ToggleSettingRow(
                                title: "Expand on Hover",
                                subtitle: "Automatically expand island when mouse pointer hovers over it",
                                isOn: $draft.expandOnHover
                            )

                            Divider().background(Color(hex: "#333338"))

                            ToggleSettingRow(
                                title: "Expand on Click",
                                subtitle: "Click compact island to toggle between collapsed and expanded",
                                isOn: $draft.expandOnClick
                            )

                            Divider().background(Color(hex: "#333338"))

                            SliderSettingRow(
                                title: "Auto-Collapse Delay",
                                subtitle: "Seconds before expanded island auto-collapses when mouse leaves",
                                value: Binding(
                                    get: { Double(draft.autoCollapseSeconds) },
                                    set: { draft.autoCollapseSeconds = Int($0) }
                                ),
                                range: 3...30,
                                step: 1,
                                format: "%.0f s"
                            )

                            Divider().background(Color(hex: "#333338"))

                            ToggleSettingRow(
                                title: "Start on macOS Login",
                                subtitle: "Automatically launch Mac Dynamic Island when your Mac starts up",
                                isOn: $draft.launchOnStartup
                            )
                        }
                        .padding(16)
                        .background(Color(hex: "#242428"))
                        .cornerRadius(12)
                        .overlay(RoundedRectangle(cornerRadius: 12).stroke(Color(hex: "#333338"), lineWidth: 1))
                    }
                }
                .padding(.horizontal, 24)
                .padding(.vertical, 16)
            }

            Divider()
                .background(Color(hex: "#2C2C2E"))

            // Bottom Action Bar
            HStack(spacing: 12) {
                Button(action: {
                    draft = MacAppSettings()
                }) {
                    Text("Reset Defaults")
                        .font(.system(size: 12.5, weight: .semibold))
                        .foregroundColor(Color(hex: "#EBEBF5"))
                        .padding(.horizontal, 16)
                        .padding(.vertical, 8)
                        .background(Color(hex: "#3A3A3C"))
                        .cornerRadius(8)
                }
                .buttonStyle(.plain)

                Spacer()

                Button(action: {
                    onClose?()
                }) {
                    Text("Cancel")
                        .font(.system(size: 12.5, weight: .semibold))
                        .foregroundColor(Color(hex: "#EBEBF5"))
                        .padding(.horizontal, 16)
                        .padding(.vertical, 8)
                        .background(Color(hex: "#3A3A3C"))
                        .cornerRadius(8)
                }
                .buttonStyle(.plain)

                Button(action: {
                    saveChanges()
                    onClose?()
                }) {
                    Text("Save Changes")
                        .font(.system(size: 12.5, weight: .semibold))
                        .foregroundColor(.white)
                        .padding(.horizontal, 18)
                        .padding(.vertical, 8)
                        .background(Color(hex: "#0A84FF"))
                        .cornerRadius(8)
                }
                .buttonStyle(.plain)
            }
            .padding(.horizontal, 24)
            .padding(.vertical, 14)
            .background(Color(hex: "#1C1C1E"))
        }
        .frame(minWidth: 560, minHeight: 640)
        .background(Color(hex: "#18181B"))
        .onAppear {
            draft = settingsManager.settings
            draft.launchOnStartup = startupService.isEnabled
        }
    }

    private func saveChanges() {
        settingsManager.save(draft)
        startupService.setEnabled(draft.launchOnStartup)
    }
}

// MARK: - Subcomponents
struct SliderSettingRow: View {
    let title: String
    let subtitle: String
    @Binding var value: Double
    let range: ClosedRange<Double>
    let step: Double
    let format: String
    var multiplier: Double = 1.0

    var body: some View {
        HStack {
            VStack(alignment: .leading, spacing: 2) {
                Text(title)
                    .font(.system(size: 13, weight: .medium))
                    .foregroundColor(.white)
                Text(subtitle)
                    .font(.system(size: 11))
                    .foregroundColor(Color(hex: "#8E8E93"))
            }

            Spacer(minLength: 20)

            HStack(spacing: 12) {
                Slider(value: $value, in: range, step: step)
                    .frame(width: 150)

                Text(String(format: format, value * multiplier))
                    .font(.system(size: 12, weight: .semibold, design: .monospaced))
                    .foregroundColor(.white)
                    .frame(width: 60, alignment: .trailing)
            }
        }
    }
}

struct ToggleSettingRow: View {
    let title: String
    let subtitle: String
    @Binding var isOn: Bool

    var body: some View {
        HStack {
            VStack(alignment: .leading, spacing: 2) {
                Text(title)
                    .font(.system(size: 13, weight: .medium))
                    .foregroundColor(.white)
                Text(subtitle)
                    .font(.system(size: 11))
                    .foregroundColor(Color(hex: "#8E8E93"))
            }

            Spacer()

            Toggle("", isOn: $isOn)
                .toggleStyle(.checkbox)
        }
    }
}
