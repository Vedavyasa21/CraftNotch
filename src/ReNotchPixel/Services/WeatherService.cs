using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Threading;
using ReNotchPixel.Models;

namespace ReNotchPixel.Services;

public class WeatherService : IDisposable
{
    private readonly HttpClient _http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
    private readonly DispatcherTimer _timer;
    private WeatherInfo _currentWeather = new WeatherInfo();

    public WeatherInfo CurrentWeather => _currentWeather;
    public event EventHandler<WeatherInfo>? WeatherUpdated;

    public WeatherService()
    {
        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMinutes(20)
        };
        _timer.Tick += (s, e) => _ = RefreshWeatherAsync();
    }

    public void Start()
    {
        _ = RefreshWeatherAsync();
        _timer.Start();
    }

    public async Task RefreshWeatherAsync()
    {
        try
        {
            // Fetch public zero-key weather using Open-Meteo
            // Defaulting coordinates or fetching based on timezone offset
            double lat = 28.61, lon = 77.20; // Default geographic center or fallback
            var offset = TimeZoneInfo.Local.BaseUtcOffset.TotalHours;
            if (Math.Abs(offset - 5.5) < 0.1) { lat = 12.97; lon = 77.59; } // IST
            else if (Math.Abs(offset - (-5)) < 0.1) { lat = 40.71; lon = -74.00; } // EST
            else if (Math.Abs(offset - (-8)) < 0.1) { lat = 37.77; lon = -122.41; } // PST
            else if (Math.Abs(offset - 0) < 0.1) { lat = 51.50; lon = -0.12; } // GMT

            string url = $"https://api.open-meteo.com/v1/forecast?latitude={lat}&longitude={lon}&current=temperature_2m,weather_code";
            var json = await _http.GetStringAsync(url);
            using var doc = JsonDocument.Parse(json);
            var current = doc.RootElement.GetProperty("current");

            double temp = current.GetProperty("temperature_2m").GetDouble();
            int code = current.GetProperty("weather_code").GetInt32();

            var info = new WeatherInfo
            {
                Temperature = $"{Math.Round(temp)}°C",
                City = TimeZoneInfo.Local.StandardName.Replace("Standard Time", "").Trim()
            };

            switch (code)
            {
                case 0:
                    info.Condition = "Clear";
                    info.Icon = "☀️";
                    break;
                case 1:
                case 2:
                case 3:
                    info.Condition = "Partly Cloudy";
                    info.Icon = "⛅";
                    break;
                case 45:
                case 48:
                    info.Condition = "Foggy";
                    info.Icon = "🌫️";
                    break;
                case 51:
                case 53:
                case 55:
                case 61:
                case 63:
                case 65:
                case 80:
                case 81:
                case 82:
                    info.Condition = "Rain";
                    info.Icon = "🌧️";
                    break;
                case 71:
                case 73:
                case 75:
                case 85:
                case 86:
                    info.Condition = "Snow";
                    info.Icon = "❄️";
                    break;
                case 95:
                case 96:
                case 99:
                    info.Condition = "Thunderstorm";
                    info.Icon = "⛈️";
                    break;
                default:
                    info.Condition = "Mild";
                    info.Icon = "🌤️";
                    break;
            }

            _currentWeather = info;
            WeatherUpdated?.Invoke(this, info);
        }
        catch
        {
            // Offline fallback
            _currentWeather = new WeatherInfo { Temperature = "23°C", Condition = "Fair", Icon = "🌤️" };
            WeatherUpdated?.Invoke(this, _currentWeather);
        }
    }

    public void Dispose()
    {
        _timer.Stop();
        _http.Dispose();
    }
}
