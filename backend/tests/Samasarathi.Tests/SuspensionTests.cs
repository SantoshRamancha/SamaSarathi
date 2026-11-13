using Samasarathi.Domain;
using Samasarathi.Domain.Entities;

namespace Samasarathi.Tests;

public class DriverSuspensionTests
{
    private const string Admin = "+919999999999";
    private static readonly DateTimeOffset Now = new(2026, 11, 13, 10, 0, 0, TimeSpan.Zero);
    private static readonly SubscriptionOptions Options = new();

    private static Driver Approved(VerificationStatus status = VerificationStatus.Approved)
    {
        var driver = new Driver { FullName = "Ravi", Phone = "+919876543210", RatePerKm = 10, VerificationStatus = status };
        driver.AddSubscription(SubscriptionKind.Paid, "Monthly", 499, 30, Now);
        return driver;
    }

    [Fact]
    public void Suspending_records_the_reason_and_who_did_it()
    {
        var driver = Approved();

        driver.Suspend("  Repeated complaints about rude behaviour  ", Admin, Now);

        Assert.Equal(VerificationStatus.Suspended, driver.VerificationStatus);
        Assert.Equal("Repeated complaints about rude behaviour", driver.RejectionReason);
        Assert.Equal(Admin, driver.ReviewedBy);
        Assert.Equal(Now, driver.ReviewedAt);
    }

    [Theory]
    [InlineData(VerificationStatus.Pending)]
    [InlineData(VerificationStatus.InReview)]
    [InlineData(VerificationStatus.Rejected)]
    [InlineData(VerificationStatus.Suspended)]
    public void Only_approved_drivers_can_be_suspended(VerificationStatus status)
    {
        var driver = Approved(status);

        Assert.Throws<InvalidOperationException>(() => driver.Suspend("Some reason", Admin, Now));
        Assert.Equal(status, driver.VerificationStatus);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Suspension_needs_a_reason(string reason)
    {
        var driver = Approved();

        Assert.Throws<InvalidOperationException>(() => driver.Suspend(reason, Admin, Now));
        Assert.Equal(VerificationStatus.Approved, driver.VerificationStatus);
    }

    [Fact]
    public void A_suspended_driver_cannot_go_online_and_is_told_why()
    {
        var driver = Approved();
        driver.Suspend("Complaints under review", Admin, Now);

        var ex = Assert.Throws<InvalidOperationException>(() => driver.GoOnline(17.36, 78.47, Now));

        Assert.Contains("suspended", ex.Message);
        Assert.Contains("Complaints under review", ex.Message);
        Assert.False(driver.IsOnline);
    }

    [Fact]
    public void A_suspended_driver_with_a_paid_subscription_still_cannot_take_rides()
    {
        var driver = Approved();
        driver.Suspend("Complaints under review", Admin, Now);

        Assert.False(driver.CanAcceptRides(Now, Options));
        Assert.True(driver.SubscriptionAt(Now, Options).CanTakeRides); // the subscription itself is fine
    }

    [Fact]
    public void Reinstating_restores_access_and_keeps_the_subscription()
    {
        var driver = Approved();
        driver.Suspend("Complaints under review", Admin, Now);

        driver.Reinstate(Admin, Now.AddDays(2));

        Assert.Equal(VerificationStatus.Approved, driver.VerificationStatus);
        Assert.Null(driver.RejectionReason);
        Assert.True(driver.CanAcceptRides(Now.AddDays(2), Options));
        driver.GoOnline(17.36, 78.47, Now.AddDays(2));
        Assert.True(driver.IsOnline);
    }

    [Theory]
    [InlineData(VerificationStatus.Approved)]
    [InlineData(VerificationStatus.InReview)]
    [InlineData(VerificationStatus.Rejected)]
    public void Only_suspended_drivers_can_be_reinstated(VerificationStatus status)
    {
        var driver = Approved(status);

        Assert.Throws<InvalidOperationException>(() => driver.Reinstate(Admin, Now));
    }

    [Fact]
    public void Suspending_does_not_by_itself_take_the_driver_offline()
    {
        // A driver with a ride under way must be able to finish it; the API takes idle drivers offline.
        var driver = Approved();
        driver.GoOnline(17.36, 78.47, Now);

        driver.Suspend("Complaints under review", Admin, Now);

        Assert.True(driver.IsOnline);
    }
}
