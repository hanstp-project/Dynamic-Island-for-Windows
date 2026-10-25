import SwiftUI
import AppKit

/// Retro Instant Camera View with Polaroid Frame, live webcam viewfinder, and filter picker
public struct RetroCameraView: View {
    @ObservedObject private var cameraService = CameraService.shared
    @State private var isShowingSavedToast: Bool = false

    public init() {}

    public var body: some View {
        HStack(spacing: 16) {
            // Left: Vintage Viewfinder Screen
            ZStack {
                // Outer Polaroid border
                RoundedRectangle(cornerRadius: 14)
                    .fill(Color(white: 0.12))
                    .frame(width: 200, height: 180)
                    .overlay(
                        RoundedRectangle(cornerRadius: 14)
                            .stroke(Color.white.opacity(0.12), lineWidth: 1)
                    )

                // Viewfinder Screen
                ZStack {
                    if let img = cameraService.capturedImage {
                        Image(nsImage: img)
                            .resizable()
                            .scaledToFill()
                            .frame(width: 180, height: 140)
                            .clipped()
                            .cornerRadius(8)
                            .colorMultiply(filterColor(cameraService.currentFilter))
                    } else {
                        // Simulated Retro Viewfinder Grid
                        ZStack {
                            Color.black
                            VStack(spacing: 6) {
                                Image(systemName: "camera.aperture")
                                    .font(.system(size: 32))
                                    .foregroundColor(.orange.opacity(0.8))
                                Text("RETRO CAM • LIVE")
                                    .font(.system(size: 10, weight: .bold, design: .monospaced))
                                    .foregroundColor(.orange)
                            }
                        }
                        .frame(width: 180, height: 140)
                        .cornerRadius(8)
                    }

                    // Viewfinder Crosshairs & Timestamp
                    VStack {
                        HStack {
                            Text("REC ●")
                                .font(.system(size: 9, weight: .bold, design: .monospaced))
                                .foregroundColor(.red)
                            Spacer()
                            Text(cameraService.currentDateStamp)
                                .font(.system(size: 9, weight: .bold, design: .monospaced))
                                .foregroundColor(.orange)
                        }
                        .padding(6)

                        Spacer()
                    }

                    // Flash White Overlay
                    if cameraService.isFlashTriggered {
                        Color.white
                            .transition(.opacity)
                    }
                }
                .frame(width: 180, height: 140)
            }

            // Right: Filter Selector & Shutter Button
            VStack(alignment: .leading, spacing: 12) {
                // Header
                HStack {
                    Text("Instant Polaroid")
                        .font(.system(size: 13, weight: .bold, design: .rounded))
                        .foregroundColor(.white)
                    Spacer()
                    Text(cameraService.currentFilter.emoji)
                        .font(.system(size: 14))
                }

                // Filter Buttons Carousel
                ScrollView(.horizontal, showsIndicators: false) {
                    HStack(spacing: 6) {
                        ForEach(RetroFilter.allCases) { filter in
                            Button(action: {
                                cameraService.currentFilter = filter
                            }) {
                                Text("\(filter.emoji) \(filter.rawValue)")
                                    .font(.system(size: 10, weight: .semibold, design: .rounded))
                                    .foregroundColor(cameraService.currentFilter == filter ? .black : .white.opacity(0.8))
                                    .padding(.horizontal, 8)
                                    .padding(.vertical, 5)
                                    .background(cameraService.currentFilter == filter ? Color.orange : Color.white.opacity(0.12))
                                    .clipShape(Capsule())
                            }
                            .buttonStyle(.plain)
                        }
                    }
                }

                Spacer()

                // Bottom: Big Red Shutter Button & Status
                HStack(spacing: 12) {
                    Button(action: {
                        cameraService.capturePhoto()
                        isShowingSavedToast = true
                        DispatchQueue.main.asyncAfter(deadline: .now() + 2.5) {
                            isShowingSavedToast = false
                        }
                    }) {
                        HStack(spacing: 8) {
                            ZStack {
                                Circle()
                                    .fill(Color.red)
                                    .frame(width: 32, height: 32)
                                Circle()
                                    .stroke(Color.white, lineWidth: 2)
                                    .frame(width: 26, height: 26)
                            }
                            Text("Ambil Foto")
                                .font(.system(size: 12, weight: .bold, design: .rounded))
                                .foregroundColor(.white)
                        }
                        .padding(.horizontal, 12)
                        .padding(.vertical, 6)
                        .background(Color.white.opacity(0.1))
                        .clipShape(Capsule())
                    }
                    .buttonStyle(.plain)

                    if isShowingSavedToast {
                        Text("Tersimpan di Pictures! 📸")
                            .font(.system(size: 10, weight: .semibold))
                            .foregroundColor(.green)
                            .transition(.opacity)
                    }
                }
            }
        }
        .padding(16)
        .onAppear {
            cameraService.start()
        }
        .onDisappear {
            cameraService.stop()
        }
    }

    private func filterColor(_ filter: RetroFilter) -> Color {
        switch filter {
        case .warm90s: return Color(red: 1.0, green: 0.92, blue: 0.8)
        case .cyberpunk: return Color(red: 0.8, green: 0.8, blue: 1.1)
        case .monochrome: return Color(white: 0.9)
        case .sepia: return Color(red: 0.95, green: 0.85, blue: 0.7)
        case .vibrant: return Color(red: 1.05, green: 1.05, blue: 1.05)
        }
    }
}
