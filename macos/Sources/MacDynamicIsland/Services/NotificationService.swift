import Foundation
import AppKit

/// Service for managing toast notification banners on the Island
@MainActor
public final class NotificationService: ObservableObject {
    public static let shared = NotificationService()

    @Published public private(set) var currentNotification: AppNotification?
    @Published public private(set) var isNotificationActive: Bool = false

    private var dismissWorkItem: DispatchWorkItem?

    public init() {}

    public func postNotification(_ notification: AppNotification) {
        dismissWorkItem?.cancel()
        currentNotification = notification
        isNotificationActive = true

        // Auto-dismiss after 5 seconds
        let work = DispatchWorkItem { [weak self] in
            Task { @MainActor [weak self] in
                self?.dismiss()
            }
        }
        dismissWorkItem = work
        DispatchQueue.main.asyncAfter(deadline: .now() + 5.0, execute: work)
    }

    public func dismiss() {
        dismissWorkItem?.cancel()
        isNotificationActive = false
        currentNotification = nil
    }

    public func triggerTestWhatsApp() {
        postNotification(AppNotification.sampleWhatsApp())
    }
}
