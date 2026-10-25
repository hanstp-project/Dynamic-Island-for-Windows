import SwiftUI

/// Live Weather Forecast subpage using Open-Meteo REST API
public struct WeatherView: View {
    @ObservedObject private var weatherService = WeatherService.shared

    public init() {}

    public var body: some View {
        HStack(spacing: 18) {
            // Left: Current Weather Condition & Temp
            VStack(alignment: .leading, spacing: 4) {
                HStack(spacing: 8) {
                    Image(systemName: weatherService.conditionIcon)
                        .font(.system(size: 28))
                        .foregroundColor(.yellow)
                    Text("\(weatherService.temperature)°C")
                        .font(.system(size: 32, weight: .bold, design: .rounded))
                        .foregroundColor(.white)
                }

                Text(weatherService.conditionText)
                    .font(.system(size: 13, weight: .semibold, design: .rounded))
                    .foregroundColor(.white.opacity(0.85))

                HStack(spacing: 12) {
                    HStack(spacing: 4) {
                        Image(systemName: "humidity.fill")
                            .foregroundColor(.cyan)
                        Text("\(weatherService.humidity)%")
                            .font(.system(size: 10, weight: .medium, design: .monospaced))
                    }
                    HStack(spacing: 4) {
                        Image(systemName: "wind")
                            .foregroundColor(.blue)
                        Text(String(format: "%.1f km/j", weatherService.windSpeedKmH))
                            .font(.system(size: 10, weight: .medium, design: .monospaced))
                    }
                }
                .foregroundColor(.white.opacity(0.5))
                .padding(.top, 4)

                Spacer()
            }
            .frame(width: 170)

            Divider()
                .background(Color.white.opacity(0.12))

            // Right: 4-Day Mini Forecast Cards
            VStack(alignment: .leading, spacing: 6) {
                HStack {
                    Text("Prakiraan Hari Ini & Esok")
                        .font(.system(size: 11, weight: .bold, design: .rounded))
                        .foregroundColor(.white.opacity(0.7))
                    Spacer()
                    Button(action: { weatherService.fetchWeather() }) {
                        Image(systemName: "arrow.clockwise")
                            .font(.system(size: 10))
                            .foregroundColor(.white.opacity(0.5))
                    }
                    .buttonStyle(.plain)
                }

                HStack(spacing: 8) {
                    if weatherService.forecast.isEmpty {
                        ForEach(["Sen", "Sel", "Rab", "Kam"], id: \.self) { day in
                            VStack(spacing: 4) {
                                Text(day)
                                    .font(.system(size: 10, weight: .bold))
                                    .foregroundColor(.white.opacity(0.6))
                                Image(systemName: "cloud.sun.fill")
                                    .foregroundColor(.yellow)
                                    .font(.system(size: 14))
                                Text("30°")
                                    .font(.system(size: 11, weight: .bold, design: .rounded))
                                    .foregroundColor(.white)
                                Text("24°")
                                    .font(.system(size: 9))
                                    .foregroundColor(.white.opacity(0.4))
                            }
                            .frame(maxWidth: .infinity)
                            .padding(.vertical, 8)
                            .background(Color.white.opacity(0.06))
                            .cornerRadius(8)
                        }
                    } else {
                        ForEach(weatherService.forecast) { day in
                            VStack(spacing: 4) {
                                Text(day.dayName)
                                    .font(.system(size: 10, weight: .bold))
                                    .foregroundColor(.white.opacity(0.6))
                                Image(systemName: day.iconName)
                                    .foregroundColor(.yellow)
                                    .font(.system(size: 14))
                                Text("\(day.tempMax)°")
                                    .font(.system(size: 11, weight: .bold, design: .rounded))
                                    .foregroundColor(.white)
                                Text("\(day.tempMin)°")
                                    .font(.system(size: 9))
                                    .foregroundColor(.white.opacity(0.4))
                            }
                            .frame(maxWidth: .infinity)
                            .padding(.vertical, 8)
                            .background(Color.white.opacity(0.06))
                            .cornerRadius(8)
                        }
                    }
                }
            }
        }
        .padding(16)
    }
}
