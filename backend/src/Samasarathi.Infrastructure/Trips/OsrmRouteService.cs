using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Samasarathi.Application.Abstractions;

namespace Samasarathi.Infrastructure.Trips;

/// <summary>
/// Road routes from OSRM (OpenStreetMap data). It sees only the pickup and drop coordinates, never who is asking.
/// The duration is free-flow time: OSRM knows nothing about live traffic. Swapping to Google Routes or Ola Maps later
/// means writing another <see cref="IRouteService"/> (their traffic-aware time would replace the peak-hour rule of thumb).
/// </summary>
public sealed class OsrmRouteService(HttpClient http, RoutingOptions options, TimeProvider clock, ILogger<OsrmRouteService> log)
    : IRouteService
{
    private const int MaxCached = 500;
    private readonly ConcurrentDictionary<string, (DateTimeOffset At, RouteInfo Route)> _cache = new();

    public async Task<RouteInfo?> TryGetRouteAsync(
        double fromLat, double fromLng, double toLat, double toLng, CancellationToken ct = default)
    {
        // ~11 m precision: repeat quotes for the same trip share one lookup.
        var key = string.Create(CultureInfo.InvariantCulture, $"{fromLat:0.0000},{fromLng:0.0000};{toLat:0.0000},{toLng:0.0000}");
        var ttl = TimeSpan.FromMinutes(options.CacheMinutes);
        if (_cache.TryGetValue(key, out var hit) && clock.GetUtcNow() - hit.At < ttl) return hit.Route;

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, BuildUrl(fromLat, fromLng, toLat, toLng));
            request.Headers.UserAgent.ParseAdd(options.UserAgent);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(options.TimeoutSeconds));

            using var response = await http.SendAsync(request, timeout.Token);
            if (!response.IsSuccessStatusCode)
            {
                log.LogWarning("Routing answered {Status}; falling back to a straight-line estimate", (int)response.StatusCode);
                return null;
            }

            var route = Parse(await response.Content.ReadAsStringAsync(timeout.Token));
            if (route is null) return null;

            if (_cache.Count >= MaxCached) _cache.Clear();
            _cache[key] = (clock.GetUtcNow(), route);
            return route;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException && !ct.IsCancellationRequested)
        {
            log.LogWarning(ex, "Routing unavailable; falling back to a straight-line estimate");
            return null;
        }
    }

    public string BuildUrl(double fromLat, double fromLng, double toLat, double toLng)
    {
        var inv = CultureInfo.InvariantCulture;
        // OSRM wants longitude first. geojson geometry avoids decoding OSRM's encoded polyline format.
        return $"{options.BaseUrl.TrimEnd('/')}/route/v1/driving/"
               + $"{fromLng.ToString(inv)},{fromLat.ToString(inv)};{toLng.ToString(inv)},{toLat.ToString(inv)}"
               + "?overview=simplified&geometries=geojson&alternatives=false&steps=false";
    }

    /// <summary>Reads OSRM's answer. Returns null when it found no route.</summary>
    public static RouteInfo? Parse(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (!root.TryGetProperty("code", out var code) || code.GetString() != "Ok") return null;
            if (!root.TryGetProperty("routes", out var routes) || routes.GetArrayLength() == 0) return null;

            var best = routes[0];
            var meters = best.GetProperty("distance").GetDouble();
            var seconds = best.GetProperty("duration").GetDouble();

            var points = new List<GeoPoint>();
            if (best.TryGetProperty("geometry", out var geometry) && geometry.TryGetProperty("coordinates", out var coords))
            {
                foreach (var c in coords.EnumerateArray())
                {
                    // GeoJSON order is [longitude, latitude].
                    if (c.GetArrayLength() >= 2) points.Add(new GeoPoint(c[1].GetDouble(), c[0].GetDouble()));
                }
            }

            return new RouteInfo(meters / 1000.0, seconds / 60.0, points);
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException or FormatException)
        {
            return null;
        }
    }
}
