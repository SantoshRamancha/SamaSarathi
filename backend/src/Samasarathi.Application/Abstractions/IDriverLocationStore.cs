namespace Samasarathi.Application.Abstractions;

public sealed record NearbyDriver(Guid DriverId, double Latitude, double Longitude, double DistanceKm);

/// <summary>"Location" config section.</summary>
public sealed class LocationOptions
{
    /// <summary>A driver who hasn't reported for this long is treated as gone (app closed, lost signal).</summary>
    public int StaleAfterSeconds { get; init; } = 60;

    /// <summary>How far around a rider to look for drivers.</summary>
    public double NearbyRadiusKm { get; init; } = 3;

    public int MaxNearbyDrivers { get; init; } = 20;
}

/// <summary>Live positions of online drivers. Updated every few seconds, so kept out of the main database.</summary>
public interface IDriverLocationStore
{
    Task UpdateAsync(Guid driverId, double latitude, double longitude, DateTimeOffset at, CancellationToken ct = default);
    Task RemoveAsync(Guid driverId, CancellationToken ct = default);

    /// <summary>Nearest first. Drivers silent for longer than <see cref="LocationOptions.StaleAfterSeconds"/> are left out.</summary>
    Task<IReadOnlyList<NearbyDriver>> FindNearbyAsync(
        double latitude, double longitude, double radiusKm, int limit, DateTimeOffset now, CancellationToken ct = default);
}
