using Samasarathi.Application.Abstractions;
using Samasarathi.Domain;
using Samasarathi.Domain.Entities;

namespace Samasarathi.Api.Controllers;

/// <summary>Works out the lines to draw on a ride's map. Shared by the rider and driver endpoints.</summary>
public static class RideRouteBuilder
{
    public static async Task<RideRouteDto> BuildAsync(IRouteService routes, Ride ride, Driver? driver, CancellationToken ct)
    {
        // Both lookups are independent, and the route service caches repeats.
        var tripTask = routes.TryGetRouteAsync(ride.Pickup.Y, ride.Pickup.X, ride.Drop.Y, ride.Drop.X, ct);

        // The driver's way to the pickup only matters until they get there.
        Task<RouteInfo?> toPickupTask = ride.Status == RideStatus.Accepted && driver?.LastKnownLocation is { } here
            ? routes.TryGetRouteAsync(here.Y, here.X, ride.Pickup.Y, ride.Pickup.X, ct)
            : Task.FromResult<RouteInfo?>(null);

        await Task.WhenAll(tripTask, toPickupTask);

        return new RideRouteDto(
            tripTask.Result?.Geometry.Select(RideRouteDto.Point).ToList() ?? [],
            toPickupTask.Result?.Geometry.Select(RideRouteDto.Point).ToList() ?? []);
    }
}
