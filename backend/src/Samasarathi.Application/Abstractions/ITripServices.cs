namespace Samasarathi.Application.Abstractions;

public sealed record GeoPoint(double Latitude, double Longitude);

/// <summary>A driving route between two points. <see cref="DurationMinutes"/> is free-flow time (no live traffic).</summary>
public sealed record RouteInfo(double DistanceKm, double DurationMinutes, IReadOnlyList<GeoPoint> Geometry);

/// <summary>Weather right now at a place.</summary>
public sealed record WeatherInfo(bool IsRaining, double PrecipitationMm);

/// <summary>"Routing" config section.</summary>
public sealed class RoutingOptions
{
    /// <summary>OSRM's public demo server: free and key-less, for development only (it is not for heavy or commercial use).</summary>
    public string BaseUrl { get; init; } = "https://router.project-osrm.org";

    public string UserAgent { get; init; } = "Samasarathi-dev";
    public int CacheMinutes { get; init; } = 10;
    public int TimeoutSeconds { get; init; } = 6;
}

/// <summary>"Weather" config section.</summary>
public sealed class WeatherOptions
{
    /// <summary>Open-Meteo: free and key-less for non-commercial use. Commercial use needs a paid plan or another provider.</summary>
    public string BaseUrl { get; init; } = "https://api.open-meteo.com";

    public string UserAgent { get; init; } = "Samasarathi-dev";

    /// <summary>At least this much rain in the last hour (mm) counts as raining. 0.2 is a light shower.</summary>
    public double RainThresholdMm { get; init; } = 0.2;

    public int CacheMinutes { get; init; } = 10;
    public int TimeoutSeconds { get; init; } = 6;

    /// <summary>Testing aid: report rain everywhere without calling the weather service. Never turn this on in production.</summary>
    public bool ForceRain { get; init; }
}

/// <summary>Driving route between two points. Returns null when it can't be worked out (service down, no road link).</summary>
public interface IRouteService
{
    Task<RouteInfo?> TryGetRouteAsync(
        double fromLatitude, double fromLongitude, double toLatitude, double toLongitude, CancellationToken ct = default);
}

/// <summary>Current weather at a point. Returns null when unknown; callers then assume dry weather.</summary>
public interface IWeatherService
{
    Task<WeatherInfo?> TryGetCurrentAsync(double latitude, double longitude, CancellationToken ct = default);
}
