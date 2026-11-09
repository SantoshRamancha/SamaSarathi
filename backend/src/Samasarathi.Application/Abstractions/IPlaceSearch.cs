namespace Samasarathi.Application.Abstractions;

/// <summary>A place a rider can pick as a destination. <see cref="Address"/> is the full line shown under the name.</summary>
public sealed record PlaceResult(string Name, string Address, double Latitude, double Longitude);

/// <summary>"PlaceSearch" config section.</summary>
public sealed class PlaceSearchOptions
{
    /// <summary>Free OpenStreetMap search. Fine for development; its usage policy rules out heavy production traffic.</summary>
    public string BaseUrl { get; init; } = "https://nominatim.openstreetmap.org";

    /// <summary>Nominatim requires every client to identify itself. Put a real contact (e.g. an email) here.</summary>
    public string UserAgent { get; init; } = "Samasarathi/0.1 (development)";

    /// <summary>Only look for places in these countries (ISO codes, comma separated).</summary>
    public string CountryCodes { get; init; } = "in";

    public int MaxResults { get; init; } = 5;

    /// <summary>Results near the rider are preferred: this is half the side of the box around them, in degrees (0.5 ≈ 50 km).</summary>
    public double NearbyBoxDegrees { get; init; } = 0.5;

    /// <summary>Nominatim allows one request per second, so outgoing searches are spaced at least this far apart.</summary>
    public int MinMillisecondsBetweenRequests { get; init; } = 1100;
}

/// <summary>Turns what a rider types ("Hitec City metro") into places with coordinates.</summary>
public interface IPlaceSearch
{
    /// <summary>Best matches first. <paramref name="nearLatitude"/>/<paramref name="nearLongitude"/> make nearby places rank higher.</summary>
    Task<IReadOnlyList<PlaceResult>> SearchAsync(
        string query, double? nearLatitude, double? nearLongitude, CancellationToken ct = default);
}

/// <summary>The search service couldn't be reached or gave an unusable answer.</summary>
public sealed class PlaceSearchUnavailableException(string message, Exception? inner = null) : Exception(message, inner);
