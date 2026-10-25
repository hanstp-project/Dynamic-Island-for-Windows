import SwiftUI

/// Time & Date subpage with digital clock, date readouts, and mini calendar
public struct TimeDateView: View {
    @State private var currentTime = Date()
    private let timer = Timer.publish(every: 1.0, on: .main, in: .common).autoconnect()

    public init() {}

    private var timeString: String {
        let formatter = DateFormatter()
        formatter.dateFormat = "HH:mm:ss"
        return formatter.string(from: currentTime)
    }

    private var dateString: String {
        let formatter = DateFormatter()
        formatter.locale = Locale(identifier: "id_ID")
        formatter.dateFormat = "EEEE, d MMMM yyyy"
        return formatter.string(from: currentTime)
    }

    private var monthYearString: String {
        let formatter = DateFormatter()
        formatter.locale = Locale(identifier: "id_ID")
        formatter.dateFormat = "MMMM yyyy"
        return formatter.string(from: currentTime)
    }

    private var calendarGridDays: [Int] {
        let calendar = Calendar.current
        guard let range = calendar.range(of: .day, in: .month, for: currentTime) else { return Array(1...30) }
        return Array(range)
    }

    private var currentDayNumber: Int {
        Calendar.current.component(.day, from: currentTime)
    }

    public var body: some View {
        HStack(spacing: 20) {
            // Left: Big Digital Clock & Date
            VStack(alignment: .leading, spacing: 4) {
                HStack(spacing: 6) {
                    Image(systemName: "clock.fill")
                        .foregroundColor(.blue)
                        .font(.system(size: 13))
                    Text(timeString)
                        .font(.system(size: 28, weight: .bold, design: .rounded))
                        .foregroundColor(.white)
                }

                Text(dateString)
                    .font(.system(size: 13, weight: .medium, design: .rounded))
                    .foregroundColor(.white.opacity(0.8))

                HStack(spacing: 6) {
                    Text(TimeZone.current.identifier)
                        .font(.system(size: 10, weight: .semibold, design: .monospaced))
                        .foregroundColor(.white.opacity(0.4))

                    Text("•")
                        .foregroundColor(.white.opacity(0.3))

                    Text("Minggu ke-\(Calendar.current.component(.weekOfYear, from: currentTime))")
                        .font(.system(size: 10, weight: .semibold))
                        .foregroundColor(.white.opacity(0.4))
                }
                .padding(.top, 4)

                Spacer()
            }
            .frame(width: 190)

            Divider()
                .background(Color.white.opacity(0.12))

            // Right: Mini Month Calendar Grid
            VStack(alignment: .leading, spacing: 8) {
                HStack {
                    Text(monthYearString)
                        .font(.system(size: 12, weight: .bold, design: .rounded))
                        .foregroundColor(.white)
                    Spacer()
                }

                // Days grid (Sun - Sat)
                HStack(spacing: 6) {
                    ForEach(["M", "S", "S", "R", "K", "J", "S"], id: \.self) { d in
                        Text(d)
                            .font(.system(size: 9, weight: .bold))
                            .foregroundColor(.white.opacity(0.4))
                            .frame(maxWidth: .infinity)
                    }
                }

                // Month days
                LazyVGrid(columns: Array(repeating: GridItem(.flexible(), spacing: 4), count: 7), spacing: 4) {
                    ForEach(calendarGridDays.prefix(21), id: \.self) { day in
                        ZStack {
                            if day == currentDayNumber {
                                Circle()
                                    .fill(Color.blue)
                                    .frame(width: 20, height: 20)
                            }
                            Text("\(day)")
                                .font(.system(size: 10, weight: day == currentDayNumber ? .bold : .regular))
                                .foregroundColor(day == currentDayNumber ? .white : .white.opacity(0.75))
                        }
                    }
                }
            }
        }
        .padding(16)
        .onReceive(timer) { newDate in
            currentTime = newDate
        }
    }
}
