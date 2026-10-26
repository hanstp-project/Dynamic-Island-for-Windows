using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace WindowsDynamicIsland.Services;

public sealed record WeatherInfo
{
    public int Temperature { get; init; } = 28;
    public string Condition { get; init; } = "Partly Cloudy";
    public string IconGlyph { get; init; } = "\uE707"; // Cloud + Sun
    public string Emoji { get; init; } = "⛅";
    public string Location { get; init; } = "Current Location";
    public string Humidity { get; init; } = "65%";
    public string WindSpeed { get; init; } = "12 km/h";
    public string HighLow { get; init; } = "H: 31°  L: 23°";
    public DateTimeOffset LastUpdated { get; init; } = DateTimeOffset.Now;
}

public sealed class WeatherService
{
    private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(5) };
    private readonly DispatcherTimer _timer;
    private WeatherInfo _current = new();

    public WeatherInfo Current => _current;
    public event Action<WeatherInfo>? Updated;

    public WeatherService()
    {
        // Refresh every 30 minutes
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(30) };
        _timer.Tick += async (_, _) => await RefreshAsync();
        _timer.Start();

        _ = Task.Run(RefreshAsync);
    }

    public async Task RefreshAsync()
    {
        try
        {
            // Free Open-Meteo API (no key required, public)
            // Default to Jakarta / Southeast Asia coordinate: Lat -6.2, Lon 106.8
            string url = "https://api.open-meteo.com/v1/forecast?latitude=-6.2&longitude=106.8&current=temperature_2m,relative_humidity_2m,weather_code,wind_speed_10m&daily=temperature_2m_max,temperature_2m_min&timezone=auto";
            var response = await _http.GetStringAsync(url).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(response);
            var root = doc.RootElement;

            if (root.TryGetProperty("current", out var currentObj))
            {
                double temp = currentObj.GetProperty("temperature_2m").GetDouble();
                int humidity = currentObj.GetProperty("relative_humidity_2m").GetInt32();
                int code = currentObj.GetProperty("weather_code").GetInt32();
                double wind = currentObj.GetProperty("wind_speed_10m").GetDouble();

                string highLow = "H: 31°  L: 23°";
                if (root.TryGetProperty("daily", out var dailyObj))
                {
                    if (dailyObj.TryGetProperty("temperature_2m_max", out var maxArr) && maxArr.GetArrayLength() > 0 &&
                        dailyObj.TryGetProperty("temperature_2m_min", out var minArr) && minArr.GetArrayLength() > 0)
                    {
                        double max = maxArr[0].GetDouble();
                        double min = minArr[0].GetDouble();
                        highLow = $"H: {Math.Round(max)}°  L: {Math.Round(min)}°";
                    }
                }

                var (condition, icon, emoji) = MapWeatherCode(code);
                _current = new WeatherInfo
                {
                    Temperature = (int)Math.Round(temp),
                    Condition = condition,
                    IconGlyph = icon,
                    Emoji = emoji,
                    Location = "Local Weather",
                    Humidity = $"{humidity}%",
                    WindSpeed = $"{Math.Round(wind)} km/h",
                    HighLow = highLow,
                    LastUpdated = DateTimeOffset.Now
                };

                Updated?.Invoke(_current);
            }
        }
        catch (Exception ex)
        {
            Log.Warn("Weather refresh encountered safe fallback.", ex);
        }
    }

    private static (string Condition, string Icon, string Emoji) MapWeatherCode(int code) => code switch
    {
        0 => ("Clear Sky", "\uE706", "☀️"),            // Sun
        1 or 2 => ("Partly Cloudy", "\uE707", "⛅"),  // Sun with cloud
        3 => ("Overcast", "\uE753", "☁️"),            // Cloud
        45 or 48 => ("Foggy", "\uE753", "🌫️"),        // Fog
        51 or 53 or 55 => ("Drizzle", "\uE708", "🌦️"),// Drizzle
        61 or 63 or 65 => ("Rain", "\uE708", "🌧️"),   // Rain
        71 or 73 or 75 => ("Snow", "\uE709", "❄️"),   // Snow
        80 or 81 or 82 => ("Showers", "\uE708", "🌧️"),// Showers
        95 or 96 or 99 => ("Thunderstorm", "\uE754", "⛈️"), // Thunder
        _ => ("Partly Cloudy", "\uE707", "⛅")
    };
}
