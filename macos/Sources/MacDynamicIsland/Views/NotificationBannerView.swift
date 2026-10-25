import SwiftUI

/// Banner view when a notification toast arrives (WhatsApp, Telegram, etc.)
public struct NotificationBannerView: View {
    @EnvironmentObject private var notifService: NotificationService
    @Binding var expansionState: IslandExpansionState

    public init(expansionState: Binding<IslandExpansionState>) {
        self._expansionState = expansionState
    }

    public var body: some View {
        if let notif = notifService.currentNotification {
            HStack(spacing: 12) {
                // App Logo / Icon
                ZStack {
                    RoundedRectangle(cornerRadius: 12)
                        .fill(Color(hex: notif.accentColorHex))
                        .frame(width: 44, height: 44)

                    Image(systemName: appIconName(for: notif.appName))
                        .font(.system(size: 20, weight: .bold))
                        .foregroundColor(.white)
                }

                // Sender and Message text
                VStack(alignment: .leading, spacing: 3) {
                    HStack {
                        Text(notif.appName)
                            .font(.system(size: 11, weight: .semibold, design: .rounded))
                            .foregroundColor(Color(hex: notif.accentColorHex))

                        Spacer()

                        Text("Baru Saja")
                            .font(.system(size: 10, weight: .regular))
                            .foregroundColor(.white.opacity(0.4))
                    }

                    Text(notif.sender)
                        .font(.system(size: 14, weight: .bold, design: .rounded))
                        .foregroundColor(.white)
                        .lineLimit(1)

                    Text(notif.message)
                        .font(.system(size: 12, weight: .medium, design: .rounded))
                        .foregroundColor(.white.opacity(0.75))
                        .lineLimit(2)
                }

                Spacer()

                // Dismiss Button
                Button(action: {
                    withAnimation(.spring(response: 0.35, dampingFraction: 0.75)) {
                        notifService.dismiss()
                        expansionState = .compact
                    }
                }) {
                    Image(systemName: "xmark.circle.fill")
                        .font(.system(size: 18))
                        .foregroundColor(.white.opacity(0.4))
                }
                .buttonStyle(.plain)
            }
            .padding(.horizontal, 16)
            .padding(.vertical, 14)
        }
    }

    private func appIconName(for app: String) -> String {
        switch app.lowercased() {
        case "whatsapp": return "message.fill"
        case "telegram": return "paperplane.fill"
        case "discord": return "bubble.left.and.bubble.right.fill"
        case "mail", "outlook": return "envelope.fill"
        default: return "bell.fill"
        }
    }
}
