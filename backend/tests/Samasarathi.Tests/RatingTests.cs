using Samasarathi.Domain;
using Samasarathi.Domain.Entities;
using Samasarathi.Domain.Pricing;

namespace Samasarathi.Tests;

public class RatingTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 2, 9, 0, 0, TimeSpan.Zero);
    private const double PickupLat = 17.3616, PickupLng = 78.4747, DropLat = 17.4435, DropLng = 78.3772;

    private static readonly PricingOptions Pricing = new();
    private static readonly FareCalculator Calculator = new(Pricing);
    private static readonly RideEstimator Estimator = new(Calculator, Pricing);

    private static (Ride Ride, Driver Driver) RideAt(RideStatus stage)
    {
        var driver = new Driver { FullName = "Ravi", Phone = "+919876543210", RatePerKm = 10, VerificationStatus = VerificationStatus.Approved };
        var ride = Ride.Request(Guid.NewGuid(), driver, PickupLat, PickupLng, DropLat, DropLng,
            Estimator.Estimate(10, PickupLat, PickupLng, DropLat, DropLng), Now);

        if (stage >= RideStatus.Accepted) ride.Accept(driver.Id, Now);
        if (stage >= RideStatus.DriverArrived) ride.MarkArrived(driver.Id);
        if (stage >= RideStatus.InProgress) ride.Start(driver.Id, Now.AddMinutes(5));
        if (stage == RideStatus.Completed) ride.Complete(driver.Id, Now.AddMinutes(30), Calculator);
        return (ride, driver);
    }

    private static (Ride Ride, Driver Driver) CompletedRide() => RideAt(RideStatus.Completed);

    [Fact]
    public void Rider_rates_the_driver_after_the_trip()
    {
        var (ride, _) = CompletedRide();

        ride.RateDriver(5, "Smooth ride", Now.AddHours(1));

        Assert.Equal(5, ride.RatingForDriver);
        Assert.Equal("Smooth ride", ride.CommentForDriver);
        Assert.Equal(Now.AddHours(1), ride.RatedDriverAt);
    }

    [Fact]
    public void Driver_rates_the_rider_after_the_trip()
    {
        var (ride, driver) = CompletedRide();

        ride.RateRider(driver.Id, 4, null, Now.AddHours(1));

        Assert.Equal(4, ride.RatingForRider);
        Assert.Null(ride.CommentForRider);
    }

    [Fact]
    public void The_two_ratings_are_independent()
    {
        var (ride, driver) = CompletedRide();

        ride.RateDriver(5, null, Now);
        Assert.Null(ride.RatingForRider);

        ride.RateRider(driver.Id, 3, null, Now);
        Assert.Equal(5, ride.RatingForDriver);
        Assert.Equal(3, ride.RatingForRider);
    }

    [Theory]
    [InlineData(RideStatus.Requested)]
    [InlineData(RideStatus.Accepted)]
    [InlineData(RideStatus.DriverArrived)]
    [InlineData(RideStatus.InProgress)]
    public void A_trip_that_is_not_finished_cannot_be_rated(RideStatus stage)
    {
        var (ride, driver) = RideAt(stage);

        Assert.Throws<InvalidOperationException>(() => ride.RateDriver(5, null, Now));
        Assert.Throws<InvalidOperationException>(() => ride.RateRider(driver.Id, 5, null, Now));
    }

    [Fact]
    public void A_cancelled_ride_cannot_be_rated()
    {
        var (ride, _) = RideAt(RideStatus.Accepted);
        ride.Cancel(byDriver: true, Now);

        Assert.Throws<InvalidOperationException>(() => ride.RateDriver(1, "No show", Now));
    }

    [Fact]
    public void A_trip_can_be_rated_only_once_per_side()
    {
        var (ride, driver) = CompletedRide();
        ride.RateDriver(5, null, Now);
        ride.RateRider(driver.Id, 5, null, Now);

        Assert.Throws<InvalidOperationException>(() => ride.RateDriver(1, "changed my mind", Now));
        Assert.Throws<InvalidOperationException>(() => ride.RateRider(driver.Id, 1, null, Now));
        Assert.Equal(5, ride.RatingForDriver);
        Assert.Equal(5, ride.RatingForRider);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    [InlineData(-1)]
    public void Stars_must_be_between_one_and_five(int stars)
    {
        var (ride, driver) = CompletedRide();

        Assert.Throws<ArgumentOutOfRangeException>(() => ride.RateDriver(stars, null, Now));
        Assert.Throws<ArgumentOutOfRangeException>(() => ride.RateRider(driver.Id, stars, null, Now));
        Assert.Null(ride.RatingForDriver);
    }

    [Fact]
    public void An_invalid_rating_does_not_use_up_the_one_chance()
    {
        var (ride, _) = CompletedRide();
        Assert.Throws<ArgumentOutOfRangeException>(() => ride.RateDriver(9, null, Now));

        ride.RateDriver(4, null, Now); // still allowed

        Assert.Equal(4, ride.RatingForDriver);
    }

    [Fact]
    public void Another_driver_cannot_rate_the_rider()
    {
        var (ride, _) = CompletedRide();
        Assert.Throws<InvalidOperationException>(() => ride.RateRider(Guid.NewGuid(), 5, null, Now));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void A_blank_comment_is_stored_as_nothing(string? comment)
    {
        var (ride, _) = CompletedRide();
        ride.RateDriver(5, comment, Now);
        Assert.Null(ride.CommentForDriver);
    }

    [Fact]
    public void A_comment_is_trimmed_and_cut_to_the_maximum_length()
    {
        var (ride, _) = CompletedRide();

        ride.RateDriver(5, "  " + new string('x', Ride.MaxCommentLength + 50) + "  ", Now);

        Assert.Equal(Ride.MaxCommentLength, ride.CommentForDriver!.Length);
        Assert.DoesNotContain(' ', ride.CommentForDriver);
    }
}
