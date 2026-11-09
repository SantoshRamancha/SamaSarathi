using Samasarathi.Domain;
using Samasarathi.Domain.Pricing;

namespace Samasarathi.Tests;

// Driver rate ₹10/km at a normal city speed of 25 km/h → ₹4.17/min.
public class FareCalculatorTests
{
    private readonly FareCalculator _calc = new(new PricingOptions());
    private static readonly TripConditions Normal = new(IsPeakHour: false, IsRaining: false);

    [Fact]
    public void Normal_traffic_charges_by_distance()
    {
        var quote = _calc.Estimate(ratePerKm: 10, distanceKm: 10, expectedMinutes: 25, Normal);

        Assert.Equal(PricingMode.Distance, quote.Mode);
        Assert.Equal(100, quote.Fare);
    }

    [Fact]
    public void Rain_with_slow_traffic_charges_by_time()
    {
        var quote = _calc.Estimate(10, 10, expectedMinutes: 60, new TripConditions(false, IsRaining: true));

        Assert.Equal(PricingMode.Time, quote.Mode);
        Assert.Equal(250, quote.Fare);
    }

    [Fact]
    public void Time_mode_never_charges_less_than_distance_fare()
    {
        var quote = _calc.Estimate(10, 10, expectedMinutes: 20, new TripConditions(IsPeakHour: true, false));

        Assert.Equal(PricingMode.Time, quote.Mode);
        Assert.Equal(100, quote.Fare);
    }

    [Theory]
    [InlineData(1.0, PricingMode.Distance)]
    [InlineData(1.29, PricingMode.Distance)]
    [InlineData(1.3, PricingMode.Time)]
    [InlineData(2.0, PricingMode.Time)]
    public void Heavy_live_traffic_switches_to_time_mode(decimal trafficRatio, PricingMode expected)
    {
        Assert.Equal(expected, _calc.ChooseMode(new TripConditions(false, false, trafficRatio)));
    }

    [Fact]
    public void Final_time_fare_is_capped_above_estimate()
    {
        // Estimated ₹250, cap is +50% = ₹375. Trip took 120 min → raw time fare ₹500.
        var quote = _calc.Final(10, 10, actualMinutes: 120, PricingMode.Time, estimatedFare: 250);

        Assert.Equal(500, quote.TimeFare);
        Assert.Equal(375, quote.Fare);
    }

    [Fact]
    public void Final_distance_fare_ignores_time()
    {
        var quote = _calc.Final(10, 12, actualMinutes: 90, PricingMode.Distance, estimatedFare: 100);

        Assert.Equal(120, quote.Fare);
    }

    [Fact]
    public void Rejects_zero_rate()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => _calc.Estimate(0, 10, 25, Normal));
    }
}
