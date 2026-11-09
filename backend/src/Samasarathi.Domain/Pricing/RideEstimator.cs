namespace Samasarathi.Domain.Pricing;

public sealed record RideEstimate(decimal DistanceKm, decimal Minutes, FareQuote Quote);

/// <summary>
/// Upfront trip estimate. With a real route (<see cref="EstimateRoute"/>) the distance and free-flow time come from the
/// routing service. If routing is unavailable, <see cref="Estimate"/> guesses the road distance from the straight line.
/// Peak hours and rain slow the expected trip down (see <see cref="TrafficOptions"/>) and switch the fare to time-based pricing.
/// </summary>
public sealed class RideEstimator(FareCalculator calculator, PricingOptions options, TrafficOptions? traffic = null)
{
    /// <summary>Roads are longer than the straight line between two points; 1.3 is a common rule of thumb for cities.</summary>
    public const double RoadFactor = 1.3;
    public const double MinTripKm = 0.2;
    public const double MaxTripKm = 100;

    private readonly TrafficOptions _traffic = traffic ?? new TrafficOptions();

    /// <summary>How many times longer than free-flow the trip is expected to take.</summary>
    public decimal Slowdown(TripConditions conditions) =>
        (conditions.IsPeakHour ? _traffic.PeakSlowdown : 1m) * (conditions.IsRaining ? _traffic.RainSlowdown : 1m);

    /// <summary>Estimate from a real route: its road distance and its free-flow travel time.</summary>
    public RideEstimate EstimateRoute(decimal ratePerKm, double roadKm, double freeFlowMinutes, TripConditions? conditions = null)
    {
        CheckTripLength(roadKm);

        var c = conditions ?? new TripConditions(IsPeakHour: false, IsRaining: false);
        var km = Math.Round((decimal)roadKm, 1);
        var minutes = Math.Max(1m, Math.Round((decimal)freeFlowMinutes * Slowdown(c), 0));
        return new RideEstimate(km, minutes, calculator.Estimate(ratePerKm, km, minutes, c));
    }

    /// <summary>Fallback when no route is available: road distance guessed from the straight line, time from the city's normal speed.</summary>
    public RideEstimate Estimate(
        decimal ratePerKm, double pickupLat, double pickupLng, double dropLat, double dropLng, TripConditions? conditions = null)
    {
        var straight = GeoMath.DistanceKm(pickupLat, pickupLng, dropLat, dropLng);
        CheckTripLength(straight);

        var c = conditions ?? new TripConditions(IsPeakHour: false, IsRaining: false);
        var km = Math.Round((decimal)(straight * RoadFactor), 1);
        var minutes = Math.Round(km / options.NormalSpeedKmph * 60m * Slowdown(c), 0);
        return new RideEstimate(km, minutes, calculator.Estimate(ratePerKm, km, minutes, c));
    }

    private static void CheckTripLength(double km)
    {
        if (km < MinTripKm)
            throw new ArgumentException("Pick a drop point a little further from your pickup.");
        if (km > MaxTripKm)
            throw new ArgumentException($"Trips are limited to {MaxTripKm:0} km.");
    }
}
