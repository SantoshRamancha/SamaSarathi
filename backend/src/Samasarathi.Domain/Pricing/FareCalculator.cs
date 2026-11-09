namespace Samasarathi.Domain.Pricing;

/// <param name="NormalSpeedKmph">City's typical speed in normal traffic; converts per-km rate to per-minute rate.</param>
/// <param name="TrafficRatioThreshold">Switch to time pricing when maps say the trip takes this many times longer than free-flow.</param>
/// <param name="MaxAboveEstimate">Final time-based fare can exceed the upfront estimate by at most this fraction.</param>
public sealed record PricingOptions(
    decimal NormalSpeedKmph = 25m,
    decimal TrafficRatioThreshold = 1.3m,
    decimal MaxAboveEstimate = 0.5m);

/// <param name="TrafficRatio">Live-traffic duration ÷ free-flow duration, from the maps API.</param>
public sealed record TripConditions(bool IsPeakHour, bool IsRaining, decimal TrafficRatio = 1m);

public sealed record FareQuote(PricingMode Mode, decimal DistanceFare, decimal TimeFare, decimal Fare);

/// <summary>
/// Normal traffic: fare = km × driver's rate.
/// Peak hour, rain or heavy traffic: fare = max(distance fare, minutes × per-minute rate),
/// so the driver is paid for time stuck in traffic but never less than the distance fare.
/// </summary>
public sealed class FareCalculator(PricingOptions options)
{
    public PricingMode ChooseMode(TripConditions conditions) =>
        conditions.IsPeakHour
        || conditions.IsRaining
        || conditions.TrafficRatio >= options.TrafficRatioThreshold
            ? PricingMode.Time
            : PricingMode.Distance;

    public decimal RatePerMinute(decimal ratePerKm) => ratePerKm * options.NormalSpeedKmph / 60m;

    /// <summary>Upfront estimate shown to the rider, using the maps API's expected trip time.</summary>
    public FareQuote Estimate(decimal ratePerKm, decimal distanceKm, decimal expectedMinutes, TripConditions conditions) =>
        Calculate(ratePerKm, distanceKm, expectedMinutes, ChooseMode(conditions), cap: null);

    /// <summary>Final fare at drop-off. Mode is the one locked at booking.</summary>
    public FareQuote Final(decimal ratePerKm, decimal distanceKm, decimal actualMinutes, PricingMode mode, decimal estimatedFare) =>
        Calculate(ratePerKm, distanceKm, actualMinutes, mode, cap: estimatedFare * (1 + options.MaxAboveEstimate));

    private FareQuote Calculate(decimal ratePerKm, decimal distanceKm, decimal minutes, PricingMode mode, decimal? cap)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(ratePerKm);
        ArgumentOutOfRangeException.ThrowIfNegative(distanceKm);
        ArgumentOutOfRangeException.ThrowIfNegative(minutes);

        var distanceFare = Round(distanceKm * ratePerKm);
        var timeFare = Round(minutes * RatePerMinute(ratePerKm));

        var fare = mode == PricingMode.Time ? Math.Max(distanceFare, timeFare) : distanceFare;
        if (mode == PricingMode.Time && cap is { } c)
            fare = Math.Max(distanceFare, Math.Min(fare, Round(c)));

        return new FareQuote(mode, distanceFare, timeFare, fare);
    }

    private static decimal Round(decimal amount) => Math.Round(amount, 0, MidpointRounding.AwayFromZero);
}
