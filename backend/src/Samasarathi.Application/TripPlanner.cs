using Samasarathi.Application.Abstractions;
using Samasarathi.Domain.Pricing;

namespace Samasarathi.Application;

public enum RouteSource
{
    /// <summary>Distance and time come from a real road route.</summary>
    Road,

    /// <summary>Routing was unavailable: distance is the straight line plus a road allowance.</summary>
    StraightLine,
}

/// <param name="WeatherKnown">False when the weather service couldn't be reached; the trip is then priced as dry.</param>
public sealed record TripPlan(
    RideEstimate Estimate, TripConditions Conditions, RouteSource Source, bool WeatherKnown, IReadOnlyList<GeoPoint> Route);

/// <summary>
/// Works out what a trip will take and cost: real route, peak hour, rain. Each outside service is optional: if routing
/// or weather is down, the rider still gets a quote (less precise) instead of an error.
/// </summary>
public sealed class TripPlanner(
    IRouteService routes, IWeatherService weather, PeakHourPolicy peakHours, RideEstimator estimator, TimeProvider clock)
{
    public async Task<TripPlan> PlanAsync(
        decimal ratePerKm, double pickupLat, double pickupLng, double dropLat, double dropLng, CancellationToken ct = default)
    {
        // Ask both at once: they don't depend on each other.
        var routeTask = routes.TryGetRouteAsync(pickupLat, pickupLng, dropLat, dropLng, ct);
        var weatherTask = weather.TryGetCurrentAsync(pickupLat, pickupLng, ct);
        await Task.WhenAll(routeTask, weatherTask);

        var route = routeTask.Result;
        var rain = weatherTask.Result;
        var conditions = new TripConditions(IsPeakHour: peakHours.IsPeak(clock.GetUtcNow()), IsRaining: rain?.IsRaining ?? false);

        return route is null
            ? new TripPlan(estimator.Estimate(ratePerKm, pickupLat, pickupLng, dropLat, dropLng, conditions),
                conditions, RouteSource.StraightLine, rain is not null, [])
            : new TripPlan(estimator.EstimateRoute(ratePerKm, route.DistanceKm, route.DurationMinutes, conditions),
                conditions, RouteSource.Road, rain is not null, route.Geometry);
    }
}
