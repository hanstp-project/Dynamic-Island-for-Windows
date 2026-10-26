import SwiftUI
import AppKit

/// Retro Instant Camera View matching Windows Edition layout
/// (Vertical camera body, 368x168 viewfinder with center 1:1 sharp & bokeh flanks, pinhole lens,
/// live HUD & glowing amber date stamp, dual-ring red shutter, 6 filter preset cards, polaroid print ejection)
public struct RetroCameraView: View {
    @ObservedObject private var cameraService = CameraService.shared
    @State private var isShowingSavedToast: Bool = false
    @State private var isEjectingPhoto: Bool = false
    @State private var printedPhoto: NSImage? = nil

    public init() {}

    public var body: some View {
        VStack(spacing: 6) {
            // Camera Body Shell
            ZStack {
                VStack(spacing: 6) {
                    // 1. Full-Width Viewfinder with Focused 1:1 Center & Bokeh Flanks
                    ZStack {
                        // Background dark chamber
                        RoundedRectangle(cornerRadius: 14)
                            .fill(Color(hex: "#0A0B0E"))
                            .frame(width: 368, height: 168)
                            .overlay(
                                RoundedRectangle(cornerRadius: 14)
                                    .stroke(Color.white.opacity(0.12), lineWidth: 1.2)
                            )

                        // Blurred Camera Underlay (covers full 368x168 with gentle bokeh blur)
                        if let img = cameraService.capturedImage {
                            Image(nsImage: img)
                                .resizable()
                                .scaledToFill()
                                .frame(width: 368, height: 168)
                                .clipped()
                                .blur(radius: 14)
                                .opacity(0.85)
                                .colorMultiply(filterMultiplyColor(cameraService.currentFilter))
                        }

                        // Vignette flank gradients on left & right
                        HStack(spacing: 0) {
                            LinearGradient(
                                colors: [Color.black.opacity(0.65), Color.clear],
                                startPoint: .leading,
                                endPoint: .trailing
                            )
                            .frame(width: 80)

                            Spacer()

                            LinearGradient(
                                colors: [Color.clear, Color.black.opacity(0.65)],
                                startPoint: .leading,
                                endPoint: .trailing
                            )
                            .frame(width: 80)
                        }
                        .frame(width: 368, height: 168)
                        .allowsHitTesting(false)

                        // Focused 1:1 Center Camera (Sharp center viewfinder)
                        Group {
                            if let img = cameraService.capturedImage {
                                Image(nsImage: img)
                                    .resizable()
                                    .scaledToFill()
                                    .frame(width: 168, height: 168)
                                    .clipped()
                                    .colorMultiply(filterMultiplyColor(cameraService.currentFilter))
                            } else {
                                // Live Camera Initializing placeholder
                                ZStack {
                                    Color.black.opacity(0.85)
                                    VStack(spacing: 6) {
                                        Image(systemName: "camera.aperture")
                                            .font(.system(size: 28))
                                            .foregroundColor(Color(hex: "#F59E0B").opacity(0.8))
                                        Text("RETRO CAM • LIVE")
                                            .font(.system(size: 9.5, weight: .bold, design: .monospaced))
                                            .foregroundColor(Color(hex: "#F59E0B"))
                                    }
                                }
                                .frame(width: 168, height: 168)
                            }
                        }
                        .frame(width: 168, height: 168)

                        // Top Center Camera Lens Pinhole Dot
                        VStack {
                            ZStack {
                                Circle()
                                    .fill(Color.black)
                                    .frame(width: 8, height: 8)
                                    .overlay(Circle().stroke(Color.gray.opacity(0.6), lineWidth: 1))
                                Circle()
                                    .fill(Color(hex: "#3B82F6"))
                                    .frame(width: 3, height: 3)
                            }
                            .padding(.top, 5)
                            Spacer()
                        }

                        // Viewfinder Flash Effect Overlay
                        if cameraService.isFlashTriggered {
                            Color.white
                                .frame(width: 368, height: 168)
                                .clipShape(RoundedRectangle(cornerRadius: 14))
                                .transition(.opacity)
                        }

                        // Bottom Viewfinder HUD
                        VStack {
                            Spacer()
                            HStack(alignment: .center) {
                                // Bottom Left: Status Live & Filter Name
                                HStack(spacing: 4) {
                                    Circle()
                                        .fill(Color(hex: "#22C55E"))
                                        .frame(width: 6, height: 6)
                                    Text("LIVE")
                                        .font(.system(size: 9, weight: .bold, design: .monospaced))
                                        .foregroundColor(Color(hex: "#22C55E"))

                                    Text("• \(cameraService.currentFilter.rawValue.uppercased())")
                                        .font(.system(size: 8.5, weight: .semibold, design: .monospaced))
                                        .foregroundColor(Color.white.opacity(0.9))
                                }

                                Spacer()

                                // Bottom Right: Vintage Glowing Amber Date Stamp
                                Text(cameraService.currentDateStamp)
                                    .font(.system(size: 11, weight: .bold, design: .monospaced))
                                    .foregroundColor(Color(hex: "#F59E0B"))
                                    .shadow(color: Color(hex: "#F59E0B").opacity(0.7), radius: 4)
                            }
                            .padding(.horizontal, 12)
                            .padding(.bottom, 8)
                        }
                        .allowsHitTesting(false)

                        // Ejected Polaroid Animation Overlay
                        if isEjectingPhoto, let photo = printedPhoto {
                            ZStack {
                                RoundedRectangle(cornerRadius: 8)
                                    .fill(Color(white: 0.96))
                                    .frame(width: 170, height: 150)
                                    .shadow(color: Color.black.opacity(0.6), radius: 12, y: 6)

                                VStack(spacing: 4) {
                                    Image(nsImage: photo)
                                        .resizable()
                                        .scaledToFill()
                                        .frame(width: 152, height: 112)
                                        .clipped()
                                        .cornerRadius(4)

                                    HStack {
                                        Text(cameraService.currentDateStamp)
                                            .font(.system(size: 8.5, weight: .bold, design: .monospaced))
                                            .foregroundColor(Color(hex: "#F59E0B"))
                                        Spacer()
                                        Text(cameraService.currentFilter.rawValue)
                                            .font(.system(size: 7.5, weight: .semibold))
                                            .foregroundColor(Color.black.opacity(0.6))
                                    }
                                    .padding(.horizontal, 10)
                                }
                            }
                            .transition(.move(edge: .top).combined(with: .opacity))
                        }
                    }
                    .frame(width: 368, height: 168)

                    // 2. Retro Control Bar: Active Filter Badge, Shutter Button, Gallery
                    HStack {
                        // Left: Active Filter Badge Indicator
                        HStack(spacing: 4) {
                            Text(cameraService.currentFilter.emoji)
                                .font(.system(size: 11))
                            Text(cameraService.currentFilter.rawValue)
                                .font(.system(size: 9.5, weight: .semibold))
                                .foregroundColor(.white)
                        }
                        .padding(.horizontal, 8)
                        .padding(.vertical, 4)
                        .background(Color.white.opacity(0.08))
                        .cornerRadius(12)
                        .overlay(RoundedRectangle(cornerRadius: 12).stroke(Color.white.opacity(0.12), lineWidth: 1))

                        Spacer()

                        // Center: Big Retro Red Mechanical Shutter Button
                        Button(action: {
                            takePictureWithAnimation()
                        }) {
                            HStack(spacing: 6) {
                                Circle()
                                    .fill(Color(hex: "#EF4444"))
                                    .frame(width: 9, height: 9)
                                    .overlay(Circle().stroke(Color.white, lineWidth: 1.2))

                                Text("SHUTTER")
                                    .font(.system(size: 10, weight: .heavy, design: .rounded))
                                    .foregroundColor(.white)
                            }
                            .padding(.horizontal, 14)
                            .padding(.vertical, 6)
                            .background(Color.white.opacity(0.12))
                            .clipShape(Capsule())
                            .overlay(
                                Capsule().stroke(Color.white.opacity(0.20), lineWidth: 1.2)
                            )
                        }
                        .buttonStyle(.plain)
                        .help("Ambil Foto & Cetak Lembaran Polaroid")

                        Spacer()

                        // Right: Open Pictures Folder Button
                        Button(action: {
                            openPicturesFolder()
                        }) {
                            HStack(spacing: 4) {
                                Text("🖼️")
                                    .font(.system(size: 11))
                                Text("Galeri")
                                    .font(.system(size: 9.5, weight: .semibold))
                                    .foregroundColor(.white)
                            }
                            .padding(.horizontal, 8)
                            .padding(.vertical, 4)
                            .background(Color.white.opacity(0.08))
                            .cornerRadius(12)
                        }
                        .buttonStyle(.plain)
                        .help("Buka Folder Foto Polaroid")
                    }
                    .frame(width: 368)
                    .padding(.vertical, 2)

                    // 3. Filter Presets Strip (6 Filter cards matching Windows)
                    ScrollView(.horizontal, showsIndicators: false) {
                        HStack(spacing: 6) {
                            ForEach(RetroFilter.allCases) { filter in
                                Button(action: {
                                    cameraService.currentFilter = filter
                                }) {
                                    VStack(spacing: 2) {
                                        ZStack {
                                            RoundedRectangle(cornerRadius: 5)
                                                .fill(filterCardBackground(filter))
                                                .frame(width: 48, height: 28)

                                            Text(filter.emoji)
                                                .font(.system(size: 14))
                                        }

                                        Text(filter.rawValue)
                                            .font(.system(size: 8, weight: cameraService.currentFilter == filter ? .bold : .medium))
                                            .foregroundColor(cameraService.currentFilter == filter ? .white : .white.opacity(0.6))
                                            .lineLimit(1)
                                    }
                                    .frame(width: 52, height: 46)
                                    .padding(2)
                                    .background(cameraService.currentFilter == filter ? Color.white.opacity(0.14) : Color.white.opacity(0.05))
                                    .cornerRadius(7)
                                    .overlay(
                                        RoundedRectangle(cornerRadius: 7)
                                            .stroke(cameraService.currentFilter == filter ? Color(hex: "#0A84FF") : Color.white.opacity(0.10), lineWidth: cameraService.currentFilter == filter ? 1.5 : 0.8)
                                    )
                                }
                                .buttonStyle(.plain)
                            }
                        }
                        .padding(.horizontal, 2)
                    }
                    .frame(width: 368, height: 48)

                    // 4. Mechanical Paper Ejection Slot Line
                    RoundedRectangle(cornerRadius: 1.2)
                        .fill(Color(hex: "#08080A"))
                        .frame(width: 180, height: 2.5)
                        .overlay(RoundedRectangle(cornerRadius: 1.2).stroke(Color.white.opacity(0.10), lineWidth: 0.8))

                    // Saved Toast Notification
                    if isShowingSavedToast {
                        Text("Tersimpan di Pictures/PolaroidPhotos! 📸")
                            .font(.system(size: 9.5, weight: .semibold, design: .rounded))
                            .foregroundColor(Color(hex: "#22C55E"))
                            .transition(.opacity)
                    }
                }
                .padding(6)
            }
            .frame(width: 384)
            .background(Color(hex: "#0E0F13").opacity(0.95))
            .cornerRadius(16)
            .overlay(
                RoundedRectangle(cornerRadius: 16)
                    .stroke(Color.white.opacity(0.10), lineWidth: 1.2)
            )
        }
        .onAppear {
            cameraService.start()
        }
        .onDisappear {
            cameraService.stop()
        }
    }

    // MARK: - Actions
    private func takePictureWithAnimation() {
        cameraService.capturePhoto()
        printedPhoto = cameraService.lastCapturedPhoto ?? cameraService.capturedImage

        withAnimation(.spring(response: 0.45, dampingFraction: 0.65)) {
            isEjectingPhoto = true
        }

        isShowingSavedToast = true

        DispatchQueue.main.asyncAfter(deadline: .now() + 3.0) {
            withAnimation(.easeInOut(duration: 0.4)) {
                isEjectingPhoto = false
            }
            isShowingSavedToast = false
        }
    }

    private func openPicturesFolder() {
        let pictures = FileManager.default.urls(for: .picturesDirectory, in: .userDomainMask).first!
        let polaroidDir = pictures.appendingPathComponent("PolaroidPhotos", isDirectory: true)
        try? FileManager.default.createDirectory(at: polaroidDir, withIntermediateDirectories: true)
        NSWorkspace.shared.open(polaroidDir)
    }

    private func filterMultiplyColor(_ filter: RetroFilter) -> Color {
        switch filter {
        case .normal: return Color.white
        case .warm90s: return Color(red: 1.0, green: 0.91, blue: 0.78)
        case .cyberpunk: return Color(red: 0.82, green: 0.85, blue: 1.15)
        case .monochrome: return Color(white: 0.92)
        case .sepia: return Color(red: 0.96, green: 0.86, blue: 0.68)
        case .vibrant: return Color(red: 1.06, green: 1.06, blue: 1.06)
        }
    }

    private func filterCardBackground(_ filter: RetroFilter) -> Color {
        switch filter {
        case .normal: return Color.gray.opacity(0.3)
        case .warm90s: return Color.orange.opacity(0.3)
        case .cyberpunk: return Color.cyan.opacity(0.3)
        case .monochrome: return Color.black.opacity(0.4)
        case .sepia: return Color.yellow.opacity(0.25)
        case .vibrant: return Color.pink.opacity(0.3)
        }
    }
}
