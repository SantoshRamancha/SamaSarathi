using Samasarathi.Domain;
using Samasarathi.Domain.Entities;
using Samasarathi.Domain.Pricing;

namespace Samasarathi.Tests;

public class RideTripTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 2, 9, 0, 0, TimeSpan.Zero);

    private const double PickupLat = 17.3616, PickupLng = 78.4747, DropLat = 17.4435, DropLng = 78.3772;

    private static readonly PricingOptions Pricing = new();
    private static readonly FareCalculator Calculator = new(Pricing);
    private static readonly RideEstimator Estimator = new(Calculator, Pricing);

    private static (Ride Ride, Driver Driver) AcceptedRide()
    {
        var driver = new Driver
        {
            FullName = "Ravi", Phone = "+919876543210", RatePerKm = 10, VerificationStatus = VerificationStatus.Approved,
        };
        var ride = Ride.Request(Guid.NewGuid(), driver, PickupLat, PickupLng, DropLat, DropLng,
            Estimator.Estimate(10, PickupLat, PickupLng, DropLat, DropLng), Now);
        ride.Accept(driver.Id, Now);
        return (ride, driver);
    }

    private static (Ride Ride, Driver Driver) InProgressRide(out DateTimeOffset startedAt)
    {
        var (ride, driver) = AcceptedRide();
        ride.MarkArrived(driver.Id);
        startedAt = Now.AddMinutes(5);
        ride.Start(driver.Id, startedAt);
        return (ride, driver);
    }

    [Fact]
    public void Driver_marks_arrival_after_accepting()
    {
        var (ride, driver) = AcceptedRide();

        ride.MarkArrived(driver.Id);

        Assert.Equal(RideStatus.DriverArrived, ride.Status);
        Assert.True(ride.IsActive);
    }

    [Fact]
    public void Arrival_cannot_be_marked_before_accepting()
    {
        var driver = new Driver { FullName = "Ravi", Phone = "+919876543210", RatePerKm = 10 };
        var ride = Ride.Request(Guid.NewGuid(), driver, PickupLat, PickupLng, DropLat, DropLng,
            Estimator.Estimate(10, PickupLat, PickupLng, DropLat, DropLng), Now);

        Assert.Throws<InvalidOperationException>(() => ride.MarkArrived(driver.Id));
    }

    [Fact]
    public void Trip_cannot_start_before_the_driver_has_arrived()
    {
        var (ride, driver) = AcceptedRide();
        Assert.Throws<InvalidOperationException>(() => ride.Start(driver.Id, Now));
    }

    [Fact]
    public void Trip_starts_after_arrival_and_records_the_start_time()
    {
        var (ride, _) = InProgressRide(out var startedAt);

        Assert.Equal(RideStatus.InProgress, ride.Status);
        Assert.Equal(startedAt, ride.StartedAt);
    }

    [Fact]
    public void Another_driver_cannot_move_the_ride_along()
    {
        var (ride, _) = AcceptedRide();
        var other = Guid.NewGuid();

        Assert.Throws<InvalidOperationException>(() => ride.MarkArrived(other));
    }

    [Fact]
    public void Completing_measures_the_time_and_sets_the_final_fare()
    {
        var (ride, driver) = InProgressRide(out var startedAt);

        ride.Complete(driver.Id, startedAt.AddMinutes(30), Calculator);

        Assert.Equal(RideStatus.Completed, ride.Status);
        Assert.Equal(startedAt.AddMinutes(30), ride.CompletedAt);
        Assert.Equal(30m, ride.ActualMinutes);
        Assert.Equal(ride.EstimatedDistanceKm, ride.ActualDistanceKm);
        Assert.False(ride.IsActive);
    }

    [Fact]
    public void Distance_priced_trip_charges_the_estimate_however_long_it_takes()
    {
        var (ride, driver) = InProgressRide(out var startedAt);
        Assert.Equal(PricingMode.Distance, ride.PricingMode);

        ride.Complete(driver.Id, startedAt.AddHours(3), Calculator);

        Assert.Equal(ride.EstimatedFare, ride.FinalFare);
    }

    [Fact]
    public void Time_priced_trip_charges_for_time_but_never_more_than_the_cap()
    {
        var (ride, driver) = InProgressRide(out var startedAt);
        ride.PricingMode = PricingMode.Time; // as if booked in rain or peak hour

        ride.Complete(driver.Id, startedAt.AddHours(5), Calculator);

        var cap = Math.Round(ride.EstimatedFare * (1 + Pricing.MaxAboveEstimate), 0, MidpointRounding.AwayFromZero);
        Assert.Equal(cap, ride.FinalFare);
    }

    [Fact]
    public void A_trip_that_has_not_started_cannot_be_completed()
    {
        var (ride, driver) = AcceptedRide();
        Assert.Throws<InvalidOperationException>(() => ride.Complete(driver.Id, Now, Calculator));
    }

    [Fact]
    public void A_completed_trip_cannot_be_completed_or_cancelled_again()
    {
        var (ride, driver) = InProgressRide(out var startedAt);
        ride.Complete(driver.Id, startedAt.AddMinutes(20), Calculator);

        Assert.Throws<InvalidOperationException>(() => ride.Complete(driver.Id, startedAt.AddMinutes(21), Calculator));
        Assert.Throws<InvalidOperationException>(() => ride.Cancel(byDriver: true, startedAt.AddMinutes(21)));
    }

    [Fact]
    public void A_trip_in_progress_cannot_be_cancelled()
    {
        var (ride, _) = InProgressRide(out var startedAt);
        Assert.Throws<InvalidOperationException>(() => ride.Cancel(byDriver: false, startedAt.AddMinutes(1)));
    }

    [Fact]
    public void Either_side_can_still_cancel_once_the_driver_has_arrived()
    {
        var (ride, driver) = AcceptedRide();
        ride.MarkArrived(driver.Id);

        ride.Cancel(byDriver: false, Now.AddMinutes(2));

        Assert.Equal(RideStatus.Cancelled, ride.Status);
    }
}
