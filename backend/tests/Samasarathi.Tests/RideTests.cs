using Samasarathi.Domain;
using Samasarathi.Domain.Entities;
using Samasarathi.Domain.Pricing;

namespace Samasarathi.Tests;

public class RideTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 2, 9, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(45);

    // Charminar -> Hitech City, roughly 13 km in a straight line.
    private const double PickupLat = 17.3616, PickupLng = 78.4747, DropLat = 17.4435, DropLng = 78.3772;

    private static readonly PricingOptions Pricing = new();
    private static readonly RideEstimator Estimator = new(new FareCalculator(Pricing), Pricing);

    private static Driver NewDriver(decimal rate = 10) =>
        new() { FullName = "Ravi", Phone = "+919876543210", RatePerKm = rate, VerificationStatus = VerificationStatus.Approved };

    private static Ride NewRide(Driver driver, Guid? riderId = null) =>
        Ride.Request(riderId ?? Guid.NewGuid(), driver, PickupLat, PickupLng, DropLat, DropLng,
            Estimator.Estimate(driver.RatePerKm, PickupLat, PickupLng, DropLat, DropLng), Now);

    // --- distance and estimate -------------------------------------------------------------

    [Fact]
    public void Distance_between_two_known_points_is_about_right()
    {
        var km = GeoMath.DistanceKm(PickupLat, PickupLng, DropLat, DropLng);
        Assert.InRange(km, 12, 14);
    }

    [Fact]
    public void Distance_to_the_same_point_is_zero() =>
        Assert.Equal(0, GeoMath.DistanceKm(PickupLat, PickupLng, PickupLat, PickupLng), 6);

    [Fact]
    public void Estimate_uses_the_drivers_rate_and_charges_by_distance()
    {
        var e = Estimator.Estimate(10, PickupLat, PickupLng, DropLat, DropLng);

        Assert.Equal(PricingMode.Distance, e.Quote.Mode);
        Assert.Equal(Math.Round(e.DistanceKm * 10, 0, MidpointRounding.AwayFromZero), e.Quote.Fare);
        Assert.True(e.Minutes > 0);
    }

    [Fact]
    public void A_higher_driver_rate_costs_more()
    {
        var cheap = Estimator.Estimate(8, PickupLat, PickupLng, DropLat, DropLng).Quote.Fare;
        var dear = Estimator.Estimate(15, PickupLat, PickupLng, DropLat, DropLng).Quote.Fare;
        Assert.True(dear > cheap);
    }

    [Fact]
    public void Estimate_road_distance_is_longer_than_the_straight_line()
    {
        var straight = (decimal)GeoMath.DistanceKm(PickupLat, PickupLng, DropLat, DropLng);
        Assert.True(Estimator.Estimate(10, PickupLat, PickupLng, DropLat, DropLng).DistanceKm > straight);
    }

    [Fact]
    public void Trip_that_is_too_short_is_refused() =>
        Assert.Throws<ArgumentException>(() => Estimator.Estimate(10, PickupLat, PickupLng, PickupLat + 0.0001, PickupLng));

    [Fact]
    public void Trip_that_is_too_long_is_refused() =>
        Assert.Throws<ArgumentException>(() => Estimator.Estimate(10, 17.38, 78.48, 28.61, 77.20)); // Hyderabad -> Delhi

    // --- requesting ------------------------------------------------------------------------

    [Fact]
    public void New_request_waits_for_the_driver_and_copies_their_rate()
    {
        var driver = NewDriver(rate: 12);
        var ride = NewRide(driver);

        Assert.Equal(RideStatus.Requested, ride.Status);
        Assert.Equal(driver.Id, ride.DriverId);
        Assert.Equal(12, ride.RatePerKm);
        Assert.True(ride.IsActive);
        Assert.Equal(PickupLat, ride.Pickup.Y); // geometry Y = latitude
        Assert.Equal(PickupLng, ride.Pickup.X);
        Assert.Equal(Now + Timeout, ride.OfferExpiresAt(Timeout));
    }

    [Fact]
    public void Changing_the_drivers_rate_later_does_not_change_the_ride()
    {
        var driver = NewDriver(rate: 10);
        var ride = NewRide(driver);
        driver.RatePerKm = 50;
        Assert.Equal(10, ride.RatePerKm);
    }

    // --- accept / decline ------------------------------------------------------------------

    [Fact]
    public void Driver_accepts_a_request()
    {
        var driver = NewDriver();
        var ride = NewRide(driver);

        ride.Accept(driver.Id, Now.AddSeconds(10));

        Assert.Equal(RideStatus.Accepted, ride.Status);
        Assert.Equal(Now.AddSeconds(10), ride.AcceptedAt);
        Assert.True(ride.IsActive);
    }

    [Fact]
    public void Another_driver_cannot_accept()
    {
        var ride = NewRide(NewDriver());
        Assert.Throws<InvalidOperationException>(() => ride.Accept(Guid.NewGuid(), Now));
    }

    [Fact]
    public void Driver_declines_a_request()
    {
        var driver = NewDriver();
        var ride = NewRide(driver);

        ride.Decline(driver.Id);

        Assert.Equal(RideStatus.Declined, ride.Status);
        Assert.False(ride.IsActive);
    }

    [Fact]
    public void An_accepted_ride_cannot_be_accepted_or_declined_again()
    {
        var driver = NewDriver();
        var ride = NewRide(driver);
        ride.Accept(driver.Id, Now);

        Assert.Throws<InvalidOperationException>(() => ride.Accept(driver.Id, Now));
        Assert.Throws<InvalidOperationException>(() => ride.Decline(driver.Id));
    }

    // --- expiry ----------------------------------------------------------------------------

    [Fact]
    public void Request_is_still_open_before_the_timeout()
    {
        var ride = NewRide(NewDriver());

        Assert.False(ride.ExpireIfStale(Now.AddSeconds(44), Timeout));
        Assert.Equal(RideStatus.Requested, ride.Status);
    }

    [Fact]
    public void Unanswered_request_expires_after_the_timeout()
    {
        var ride = NewRide(NewDriver());

        Assert.True(ride.ExpireIfStale(Now.AddSeconds(45), Timeout));
        Assert.Equal(RideStatus.Expired, ride.Status);
        Assert.False(ride.IsActive);
    }

    [Fact]
    public void An_accepted_ride_never_expires()
    {
        var driver = NewDriver();
        var ride = NewRide(driver);
        ride.Accept(driver.Id, Now);

        Assert.False(ride.ExpireIfStale(Now.AddHours(1), Timeout));
        Assert.Equal(RideStatus.Accepted, ride.Status);
    }

    [Fact]
    public void An_expired_request_cannot_be_accepted()
    {
        var driver = NewDriver();
        var ride = NewRide(driver);
        ride.ExpireIfStale(Now.AddMinutes(5), Timeout);

        Assert.Throws<InvalidOperationException>(() => ride.Accept(driver.Id, Now.AddMinutes(5)));
    }

    // --- cancelling ------------------------------------------------------------------------

    [Fact]
    public void Rider_can_cancel_a_waiting_request()
    {
        var ride = NewRide(NewDriver());

        ride.Cancel(byDriver: false, Now.AddSeconds(5));

        Assert.Equal(RideStatus.Cancelled, ride.Status);
        Assert.Equal("Cancelled by rider", ride.CancelReason);
        Assert.Equal(Now.AddSeconds(5), ride.CancelledAt);
        Assert.False(ride.IsActive);
    }

    [Fact]
    public void Driver_can_cancel_after_accepting()
    {
        var driver = NewDriver();
        var ride = NewRide(driver);
        ride.Accept(driver.Id, Now);

        ride.Cancel(byDriver: true, Now.AddMinutes(1));

        Assert.Equal(RideStatus.Cancelled, ride.Status);
        Assert.Equal("Cancelled by driver", ride.CancelReason);
    }

    [Theory]
    [InlineData(RideStatus.Cancelled)]
    [InlineData(RideStatus.Declined)]
    [InlineData(RideStatus.Expired)]
    [InlineData(RideStatus.Completed)]
    [InlineData(RideStatus.InProgress)]
    public void A_finished_or_started_ride_cannot_be_cancelled(RideStatus status)
    {
        var ride = NewRide(NewDriver());
        ride.Status = status;

        Assert.Throws<InvalidOperationException>(() => ride.Cancel(byDriver: false, Now));
    }
}
