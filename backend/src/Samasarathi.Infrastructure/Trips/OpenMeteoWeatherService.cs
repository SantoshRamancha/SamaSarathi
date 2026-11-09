using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Samasarathi.Application.Abstractions;

namespace Samasarathi.Infrastructure.Trips;

/// <summary>
/// Current rain from Open-Meteo. It sees only the pickup coordinates, rounded to about 1 km, never who is asking.
/// To switch provider (e.g. OpenWeatherMap or Tomorrow.io) write another <see cref="IWeatherService"/>.
/// </summary>
public sealed class OpenMeteoWeatherService(
    HttpClient http, WeatherOptions options, TimeProvider clock, ILogger<OpenMeteoWeatherService> log) : IWeatherService
{
    private const int MaxCached = 500;
    private readonly ConcurrentDictionary<string, (DateTimeOffset At, WeatherInfo Weather)> _cache = new();

    public async Task<WeatherInfo?> TryGetCurrentAsync(double latitude, double longitude, CancellationToken ct = default)
    {
        if (options.ForceRain) return new WeatherInfo(true, 5);

        // Weather doesn't change across a street: ~1 km precision, so nearby riders share one lookup.
        var key = string.Create(CultureInfo.InvariantCulture, $"{latitude:0.00},{longitude:0.00}");
        var ttl = TimeSpan.FromMinutes(options.CacheMinutes);
        if (_cache.TryGetValue(key, out var hit) && clock.GetUtcNow() - hit.At < ttl) return hit.Weather;

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, BuildUrl(latitude, longitude));
            request.Headers.UserAgent.ParseAdd(options.UserAgent);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(options.TimeoutSeconds));

            using var response = await http.SendAsync(request, timeout.Token);
            if (!response.IsSuccessStatusCode)
            {
                log.LogWarning("Weather answered {Status}; assuming dry weather", (int)response.StatusCode);
                return null;
            }

            var weather = Parse(await response.Content.ReadAsStringAsync(timeout.Token), options.RainThresholdMm);
            if (weather is null) return null;

            if (_cache.Count >= MaxCached) _cache.Clear();
            _cache[key] = (clock.GetUtcNow(), weather);
            return weather;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException && !ct.IsCancellationRequested)
        {
            log.LogWarning(ex, "Weather unavailable; assuming dry weather");
            return null;
        }
    }

    public string BuildUrl(double latitude, double longitude)
    {
        var inv = CultureInfo.InvariantCulture;
        return $"{options.BaseUrl.TrimEnd('/')}/v1/forecast?latitude={latitude.ToString("0.####", inv)}"
               + $"&longitude={longitude.ToString("0.####", inv)}&current=precipitation,weather_code&timezone=auto";
    }

    /// <summary>
    /// Raining if enough rain fell recently, or the weather code says drizzle / rain / showers / thunderstorm
    /// (WMO codes 51–67, 80–82 and 95–99). Returns null when the answer has no current weather.
    /// </summary>
    public static WeatherInfo? Parse(string json, double rainThresholdMm)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("current", out var current)) return null;

            var mm = current.TryGetProperty("precipitation", out var p) && p.ValueKind == JsonValueKind.Number ? p.GetDouble() : 0;
            var code = current.TryGetProperty("weather_code", out var c) && c.ValueKind == JsonValueKind.Number ? c.GetInt32() : -1;

            var wetCode = code is >= 51 and <= 67 or >= 80 and <= 82 or >= 95 and <= 99;
            return new WeatherInfo(mm >= rainThresholdMm || wetCode, mm);
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException)
        {
            return null;
        }
    }
}
