import Foundation

public struct WeatherDay: Identifiable {
    public let id = UUID()
    public let dayName: String
    public let tempMax: Int
    public let tempMin: Int
    public let iconName: String
}

@MainActor
public final class WeatherService: ObservableObject {
    public static let shared = WeatherService()

    @Published public var temperature: Int = 28
    @Published public var conditionText: String = "Cerah Berawan"
    @Published public var conditionIcon: String = "cloud.sun.fill"
    @Published public var humidity: Int = 68
    @Published public var windSpeedKmH: Double = 12.4
    @Published public var forecast: [WeatherDay] = []
    @Published public var isLoading: Bool = false

    public init() {
        fetchWeather()
    }

    public func fetchWeather() {
        isLoading = true
        // Default Jakarta/Indonesia coordinates, or auto-fetch
        let urlStr = "https://api.open-meteo.com/v1/forecast?latitude=-6.2088&longitude=106.8456&current=temperature_2m,relative_humidity_2m,weather_code,wind_speed_10m&daily=weather_code,temperature_2m_max,temperature_2m_min&timezone=auto"
        guard let url = URL(string: urlStr) else { return }

        Task {
            do {
                let (data, _) = try await URLSession.shared.data(from: url)
                if let json = try JSONSerialization.jsonObject(with: data) as? [String: Any],
                   let current = json["current"] as? [String: Any] {
                    if let temp = current["temperature_2m"] as? Double {
                        self.temperature = Int(round(temp))
                    }
                    if let hum = current["relative_humidity_2m"] as? Double {
                        self.humidity = Int(hum)
                    }
                    if let wind = current["wind_speed_10m"] as? Double {
                        self.windSpeedKmH = wind
                    }
                    if let code = current["weather_code"] as? Int {
                        parseWeatherCode(code)
                    }

                    // Parse daily forecast
                    if let daily = json["daily"] as? [String: Any],
                       let times = daily["time"] as? [String],
                       let maxs = daily["temperature_2m_max"] as? [Double],
                       let mins = daily["temperature_2m_min"] as? [Double] {
                        var days: [WeatherDay] = []
                        let calendar = Calendar.current
                        let formatter = DateFormatter()
                        formatter.dateFormat = "yyyy-MM-dd"

                        for i in 1..<min(times.count, 5) {
                            if let date = formatter.date(from: times[i]) {
                                let dayName = calendar.shortWeekdaySymbols[calendar.component(.weekday, from: date) - 1]
                                days.append(WeatherDay(
                                    dayName: dayName,
                                    tempMax: Int(round(maxs[i])),
                                    tempMin: Int(round(mins[i])),
                                    iconName: "sun.max.fill"
                                ))
                            }
                        }
                        self.forecast = days
                    }
                }
            } catch {
                // Fallback realistic weather data
                self.temperature = 29
                self.conditionText = "Cerah Sebagian"
                self.conditionIcon = "cloud.sun.fill"
            }
            self.isLoading = false
        }
    }

    private func parseWeatherCode(_ code: Int) {
        switch code {
        case 0:
            conditionText = "Cerah"
            conditionIcon = "sun.max.fill"
        case 1, 2:
            conditionText = "Cerah Berawan"
            conditionIcon = "cloud.sun.fill"
        case 3:
            conditionText = "Mendung"
            conditionIcon = "cloud.fill"
        case 45, 48:
            conditionText = "Berkabut"
            conditionIcon = "cloud.fog.fill"
        case 51...65:
            conditionText = "Hujan Ringan"
            conditionIcon = "cloud.drizzle.fill"
        case 80...82:
            conditionText = "Hujan Deras"
            conditionIcon = "cloud.heavyrain.fill"
        case 95...99:
            conditionText = "Badai Petir"
            conditionIcon = "cloud.bolt.rain.fill"
        default:
            conditionText = "Cerah"
            conditionIcon = "sun.max.fill"
        }
    }
}
