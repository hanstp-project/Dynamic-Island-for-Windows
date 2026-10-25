import Foundation
import AppKit
import AVFoundation
import CoreImage
import CoreMedia

public enum RetroFilter: String, CaseIterable, Identifiable {
    case warm90s = "Warm '90s"
    case cyberpunk = "Cyberpunk"
    case monochrome = "B&W Film"
    case sepia = "Sepia Gold"
    case vibrant = "Vibrant Fuji"

    public var id: String { rawValue }

    public var emoji: String {
        switch self {
        case .warm90s: return "🎞️"
        case .cyberpunk: return "🌆"
        case .monochrome: return "🖤"
        case .sepia: return "📜"
        case .vibrant: return "🌈"
        }
    }
}

/// Retro Instant Camera service for macOS with live webcam capture & Polaroid generation
@MainActor
public final class CameraService: NSObject, ObservableObject, AVCaptureVideoDataOutputSampleBufferDelegate {
    public static let shared = CameraService()

    @Published public var currentFilter: RetroFilter = .warm90s
    @Published public var capturedImage: NSImage?
    @Published public var lastCapturedPhoto: NSImage?
    @Published public var isFlashTriggered: Bool = false
    @Published public var isRunning: Bool = false
    @Published public var lastSavedPath: String = ""

    private var captureSession: AVCaptureSession?
    private var videoOutput: AVCaptureVideoDataOutput?
    private static let ciContext = CIContext()

    public var currentDateStamp: String {
        let formatter = DateFormatter()
        formatter.dateFormat = "'yy MM dd"
        return formatter.string(from: Date())
    }

    public override init() {
        super.init()
    }

    public func start() {
        guard !isRunning else { return }
        isRunning = true
        setupCameraSession()
    }

    public func stop() {
        isRunning = false
        captureSession?.stopRunning()
        captureSession = nil
    }

    private func setupCameraSession() {
        let status = AVCaptureDevice.authorizationStatus(for: .video)
        if status == .authorized {
            configureSession()
        } else if status == .notDetermined {
            AVCaptureDevice.requestAccess(for: .video) { [weak self] granted in
                if granted {
                    Task { @MainActor [weak self] in
                        self?.configureSession()
                    }
                }
            }
        }
    }

    private func configureSession() {
        let session = AVCaptureSession()
        session.sessionPreset = .medium

        // Discover camera on macOS (built-in or external)
        let discovery = AVCaptureDevice.DiscoverySession(
            deviceTypes: [.builtInWideAngleCamera, .externalUnknown],
            mediaType: .video,
            position: .unspecified
        )

        guard let camera = discovery.devices.first ?? AVCaptureDevice.default(for: .video),
              let input = try? AVCaptureDeviceInput(device: camera),
              session.canAddInput(input) else {
            return
        }

        session.addInput(input)

        let output = AVCaptureVideoDataOutput()
        output.videoSettings = [kCVPixelBufferPixelFormatTypeKey as String: Int(kCVPixelFormatType_32BGRA)]
        output.setSampleBufferDelegate(self, queue: DispatchQueue(label: "camera.stream", qos: .userInteractive))

        if session.canAddOutput(output) {
            session.addOutput(output)
            self.videoOutput = output
        }

        DispatchQueue.global(qos: .userInitiated).async {
            session.startRunning()
        }
        self.captureSession = session
    }

    nonisolated public func captureOutput(
        _ output: AVCaptureOutput,
        didOutput sampleBuffer: CMSampleBuffer,
        from connection: AVCaptureConnection
    ) {
        guard let cvImage = CMSampleBufferGetImageBuffer(sampleBuffer) else { return }
        let ciImage = CIImage(cvImageBuffer: cvImage)
        if let cgImage = Self.ciContext.createCGImage(ciImage, from: ciImage.extent) {
            let nsImage = NSImage(cgImage: cgImage, size: NSSize(width: ciImage.extent.width, height: ciImage.extent.height))
            Task { @MainActor in
                self.capturedImage = nsImage
            }
        }
    }

    public func capturePhoto() {
        isFlashTriggered = true
        DispatchQueue.main.asyncAfter(deadline: .now() + 0.15) { [weak self] in
            self?.isFlashTriggered = false
        }

        // Render Polaroid frame
        savePolaroidToPictures()
    }

    private func savePolaroidToPictures() {
        let pictures = FileManager.default.urls(for: .picturesDirectory, in: .userDomainMask).first!
        let polaroidDir = pictures.appendingPathComponent("PolaroidPhotos", isDirectory: true)

        try? FileManager.default.createDirectory(at: polaroidDir, withIntermediateDirectories: true)

        let filename = "POLAROID_\(Int(Date().timeIntervalSince1970)).png"
        let fileURL = polaroidDir.appendingPathComponent(filename)

        // Create bitmap
        let size = NSSize(width: 400, height: 480)
        let image = NSImage(size: size)
        image.lockFocus()

        // Classic Polaroid white border
        NSColor(white: 0.96, alpha: 1.0).setFill()
        NSRect(origin: .zero, size: size).fill()

        // Photo cutout
        let photoRect = NSRect(x: 24, y: 90, width: 352, height: 360)
        NSColor.darkGray.setFill()
        photoRect.fill()

        if let current = capturedImage {
            current.draw(in: photoRect)
        } else {
            // Draw gradient if live camera stream is still initializing
            let gradient = NSGradient(starting: NSColor.systemPurple, ending: NSColor.systemOrange)
            gradient?.draw(in: photoRect, angle: 45)
        }

        // Orange timestamp watermark
        let stamp = "\(currentDateStamp) • \(currentFilter.rawValue.uppercased())"
        let attrs: [NSAttributedString.Key: Any] = [
            .font: NSFont.monospacedDigitSystemFont(ofSize: 13, weight: .bold),
            .foregroundColor: NSColor.systemOrange
        ]
        let attrString = NSAttributedString(string: stamp, attributes: attrs)
        attrString.draw(at: NSPoint(x: 32, y: 35))

        image.unlockFocus()

        self.lastCapturedPhoto = image

        if let tiff = image.tiffRepresentation,
           let rep = NSBitmapImageRep(data: tiff),
           let png = rep.representation(using: .png, properties: [:]) {
            try? png.write(to: fileURL)
            self.lastSavedPath = fileURL.path
        }
    }
}
