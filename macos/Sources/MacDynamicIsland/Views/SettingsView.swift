import SwiftUI
import AppKit

/// Settings Subpage allowing customization of display mode, startup, test notifications, and app controls
public struct SettingsView: View {
    @EnvironmentObject private var windowController: WindowController
    @ObservedObject private var notifService = NotificationService.shared
    @ObservedObject private var startupService = StartupService.shared

    public init() {}

    public var body: some View {
        ScrollView(.vertical, showsIndicators: false) {
            VStack(alignment: .leading, spacing: 14) {
                // Header
                HStack {
                    HStack(spacing: 6) {
                        Image(systemName: "gearshape.fill")
                            .font(.system(size: 13, weight: .bold))
                            .foregroundColor(.orange)
                        Text("Pengaturan Dynamic Island")
                            .font(.system(size: 13, weight: .bold, design: .rounded))
                            .foregroundColor(.white)
                    }
                    Spacer()
                    Text("v1.0.0 macOS")
                        .font(.system(size: 10, design: .monospaced))
                        .foregroundColor(.white.opacity(0.4))
                }

                Divider()
                    .background(Color.white.opacity(0.12))

                // Section 1: Mode Tampilan Layar
                VStack(alignment: .leading, spacing: 8) {
                    Text("MODE TAMPILAN")
                        .font(.system(size: 10, weight: .bold, design: .rounded))
                        .foregroundColor(.white.opacity(0.5))

                    HStack(spacing: 10) {
                        ModeSelectCard(
                            title: "MacBook Notch",
                            subtitle: "Menempel di Bezel Kamera",
                            icon: "laptopcomputer",
                            isSelected: windowController.currentDisplayMode == .macBookNotch
                        ) {
                            withAnimation(.spring(response: 0.35, dampingFraction: 0.75)) {
                                if windowController.currentDisplayMode != .macBookNotch {
                                    windowController.toggleDisplayMode()
                                }
                            }
                        }

                        ModeSelectCard(
                            title: "Dynamic Island",
                            subtitle: "Melayang di Bawah Kamera",
                            icon: "capsule.fill",
                            isSelected: windowController.currentDisplayMode == .dynamicIsland
                        ) {
                            withAnimation(.spring(response: 0.35, dampingFraction: 0.75)) {
                                if windowController.currentDisplayMode != .dynamicIsland {
                                    windowController.toggleDisplayMode()
                                }
                            }
                        }
                    }
                }

                // Section 2: Preferensi Sistem
                VStack(alignment: .leading, spacing: 8) {
                    Text("PREFERENSI")
                        .font(.system(size: 10, weight: .bold, design: .rounded))
                        .foregroundColor(.white.opacity(0.5))

                    HStack {
                        VStack(alignment: .leading, spacing: 2) {
                            Text("Buka Otomatis saat Mac Dinyalakan")
                                .font(.system(size: 12, weight: .medium))
                                .foregroundColor(.white)
                            Text("Aplikasi langsung aktif saat login ke macOS")
                                .font(.system(size: 10))
                                .foregroundColor(.white.opacity(0.45))
                        }
                        Spacer()
                        Toggle("", isOn: Binding(
                            get: { startupService.isEnabled },
                            set: { startupService.setEnabled($0) }
                        ))
                        .toggleStyle(.switch)
                    }
                    .padding(10)
                    .background(Color.white.opacity(0.06))
                    .cornerRadius(10)
                }

                // Section 3: Uji Coba Notifikasi Toast
                VStack(alignment: .leading, spacing: 8) {
                    Text("UJI COBA NOTIFIKASI TOAST")
                        .font(.system(size: 10, weight: .bold, design: .rounded))
                        .foregroundColor(.white.opacity(0.5))

                    HStack(spacing: 8) {
                        Button(action: {
                            notifService.postNotification(AppNotification.sampleWhatsApp())
                        }) {
                            HStack(spacing: 4) {
                                Image(systemName: "message.fill")
                                Text("WhatsApp")
                            }
                            .font(.system(size: 11, weight: .semibold))
                            .foregroundColor(.white)
                            .padding(.horizontal, 10)
                            .padding(.vertical, 6)
                            .background(Color.green.opacity(0.8))
                            .clipShape(Capsule())
                        }
                        .buttonStyle(.plain)

                        Button(action: {
                            notifService.postNotification(AppNotification.sampleTelegram())
                        }) {
                            HStack(spacing: 4) {
                                Image(systemName: "paperplane.fill")
                                Text("Telegram")
                            }
                            .font(.system(size: 11, weight: .semibold))
                            .foregroundColor(.white)
                            .padding(.horizontal, 10)
                            .padding(.vertical, 6)
                            .background(Color.blue.opacity(0.8))
                            .clipShape(Capsule())
                        }
                        .buttonStyle(.plain)

                        Button(action: {
                            notifService.postNotification(AppNotification.sampleSpotify())
                        }) {
                            HStack(spacing: 4) {
                                Image(systemName: "music.note")
                                Text("Music")
                            }
                            .font(.system(size: 11, weight: .semibold))
                            .foregroundColor(.white)
                            .padding(.horizontal, 10)
                            .padding(.vertical, 6)
                            .background(Color.purple.opacity(0.8))
                            .clipShape(Capsule())
                        }
                        .buttonStyle(.plain)

                        Button(action: {
                            notifService.postNotification(AppNotification.sampleBattery())
                        }) {
                            HStack(spacing: 4) {
                                Image(systemName: "battery.25")
                                Text("Baterai")
                            }
                            .font(.system(size: 11, weight: .semibold))
                            .foregroundColor(.white)
                            .padding(.horizontal, 10)
                            .padding(.vertical, 6)
                            .background(Color.red.opacity(0.8))
                            .clipShape(Capsule())
                        }
                        .buttonStyle(.plain)
                    }
                }

                // Section 4: Folder Foto & Keluar
                HStack(spacing: 12) {
                    Button(action: {
                        let pictures = FileManager.default.urls(for: .picturesDirectory, in: .userDomainMask).first!
                        let polaroidDir = pictures.appendingPathComponent("PolaroidPhotos")
                        try? FileManager.default.createDirectory(at: polaroidDir, withIntermediateDirectories: true)
                        NSWorkspace.shared.open(polaroidDir)
                    }) {
                        HStack(spacing: 6) {
                            Image(systemName: "folder.fill")
                            Text("Buka Folder Foto Polaroid")
                        }
                        .font(.system(size: 11, weight: .semibold))
                        .foregroundColor(.white)
                        .padding(.horizontal, 12)
                        .padding(.vertical, 6)
                        .background(Color.white.opacity(0.12))
                        .clipShape(Capsule())
                    }
                    .buttonStyle(.plain)

                    Spacer()

                    Button(action: {
                        NSApplication.shared.terminate(nil)
                    }) {
                        HStack(spacing: 6) {
                            Image(systemName: "power")
                            Text("Keluar (Quit)")
                        }
                        .font(.system(size: 11, weight: .bold))
                        .foregroundColor(.red)
                        .padding(.horizontal, 12)
                        .padding(.vertical, 6)
                        .background(Color.red.opacity(0.15))
                        .clipShape(Capsule())
                    }
                    .buttonStyle(.plain)
                }
                .padding(.top, 4)
            }
            .padding(16)
        }
    }
}

struct ModeSelectCard: View {
    let title: String
    let subtitle: String
    let icon: String
    let isSelected: Bool
    let action: () -> Void

    var body: some View {
        Button(action: action) {
            VStack(alignment: .leading, spacing: 6) {
                HStack {
                    Image(systemName: icon)
                        .font(.system(size: 16))
                        .foregroundColor(isSelected ? .orange : .white.opacity(0.5))
                    Spacer()
                    if isSelected {
                        Image(systemName: "checkmark.circle.fill")
                            .foregroundColor(.orange)
                    }
                }
                Text(title)
                    .font(.system(size: 12, weight: .bold))
                    .foregroundColor(.white)
                Text(subtitle)
                    .font(.system(size: 9))
                    .foregroundColor(.white.opacity(0.5))
            }
            .padding(10)
            .frame(maxWidth: .infinity, alignment: .leading)
            .background(isSelected ? Color.orange.opacity(0.15) : Color.white.opacity(0.06))
            .overlay(
                RoundedRectangle(cornerRadius: 10)
                    .stroke(isSelected ? Color.orange : Color.clear, lineWidth: 1.5)
            )
            .cornerRadius(10)
        }
        .buttonStyle(.plain)
    }
}
