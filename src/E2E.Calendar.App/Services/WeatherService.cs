using System.Net.Http.Json;
using System.Runtime.InteropServices;
using Windows.Devices.Geolocation;

namespace E2E.Calendar.App.Services;

public sealed record WeatherSnapshot(string Location, double TemperatureCelsius, int WeatherCode, DateTimeOffset UpdatedUtc)
{
    public string Summary => WeatherCode switch
    {
        0 => "Clear",
        1 or 2 or 3 => "Partly cloudy",
        45 or 48 => "Foggy",
        >= 51 and <= 67 => "Rain",
        >= 71 and <= 86 => "Snow",
        >= 95 => "Storm",
        _ => "Unknown",
    };

    public string Display => $"{Location} · {TemperatureCelsius:0}°C · {Summary}";
}

public sealed class WeatherService
{
    private readonly HttpClient _http;

    public WeatherService(HttpClient http)
    {
        _http = http;
    }

    public async Task<WeatherSnapshot?> GetLocalAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var access = await Geolocator.RequestAccessAsync();
            if (access != GeolocationAccessStatus.Allowed)
            {
                return null;
            }

            var position = await new Geolocator { DesiredAccuracyInMeters = 1000 }
                .GetGeopositionAsync()
                .AsTask(cancellationToken);
            var latitude = position.Coordinate.Point.Position.Latitude;
            var longitude = position.Coordinate.Point.Position.Longitude;
            var result = await _http.GetFromJsonAsync<OpenMeteoResponse>(
                $"https://api.open-meteo.com/v1/forecast?latitude={latitude:0.####}&longitude={longitude:0.####}&current=temperature_2m,weather_code&timezone=auto",
                cancellationToken);

            if (result?.Current is null)
            {
                return null;
            }

            return new WeatherSnapshot(
                "Local weather",
                result.Current.Temperature,
                result.Current.WeatherCode,
                DateTimeOffset.UtcNow);
        }
        catch (HttpRequestException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
        catch (COMException)
        {
            return null;
        }
    }

    private sealed record OpenMeteoResponse(CurrentWeather? Current);

    private sealed record CurrentWeather(
        [property: System.Text.Json.Serialization.JsonPropertyName("temperature_2m")] double Temperature,
        [property: System.Text.Json.Serialization.JsonPropertyName("weather_code")] int WeatherCode);
}
