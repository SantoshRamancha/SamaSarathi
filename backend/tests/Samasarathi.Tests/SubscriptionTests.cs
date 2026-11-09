using Samasarathi.Domain;
using Samasarathi.Domain.Entities;

namespace Samasarathi.Tests;

public class SubscriptionTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 9, 9, 0, 0, TimeSpan.Zero);
    private static readonly SubscriptionOptions Options = new() { TrialDays = 30, GraceDays = 3, ExpiryWarningDays = 3, PlanDays = 30 };

    private static Driver Approved() =>
        new() { FullName = "Ravi", Phone = "+919876543210", RatePerKm = 10, VerificationStatus = VerificationStatus.Approved };

    [Fact]
    public void New_driver_has_no_subscription_and_cannot_take_rides()
    {
        var state = Approved().SubscriptionAt(Now, Options);

        Assert.Equal(SubscriptionPhase.None, state.Phase);
        Assert.False(state.CanTakeRides);
    }

    [Fact]
    public void Trial_starts_once_for_an_approved_driver()
    {
        var driver = Approved();

        Assert.True(driver.StartTrialIfNew(Now, Options));
        Assert.False(driver.StartTrialIfNew(Now.AddDays(1), Options)); // never a second trial

        var state = driver.SubscriptionAt(Now, Options);
        Assert.Equal(SubscriptionPhase.Active, state.Phase);
        Assert.True(state.IsTrial);
        Assert.Equal(Now.AddDays(30), state.ValidUntil);
        Assert.Single(driver.Subscriptions);
    }

    [Theory]
    [InlineData(VerificationStatus.Pending)]
    [InlineData(VerificationStatus.InReview)]
    [InlineData(VerificationStatus.Rejected)]
    public void Trial_does_not_start_before_approval(VerificationStatus status)
    {
        var driver = Approved();
        driver.VerificationStatus = status;

        Assert.False(driver.StartTrialIfNew(Now, Options));
        Assert.Empty(driver.Subscriptions);
    }

    [Fact]
    public void Trial_can_be_switched_off_in_config()
    {
        var driver = Approved();

        Assert.False(driver.StartTrialIfNew(Now, new SubscriptionOptions { TrialDays = 0 }));
    }

    [Fact]
    public void Days_left_counts_down_and_warning_appears_near_the_end()
    {
        var driver = Approved();
        driver.AddSubscription(SubscriptionKind.Paid, "Monthly", 499, 30, Now);

        var early = driver.SubscriptionAt(Now.AddDays(10), Options);
        Assert.Equal(19, early.DaysLeft(Now.AddDays(10)));
        Assert.False(early.ExpiringSoon);

        var late = driver.SubscriptionAt(Now.AddDays(28), Options);
        Assert.True(late.ExpiringSoon);
        Assert.Equal(SubscriptionPhase.Active, late.Phase);
    }

    [Fact]
    public void After_the_end_the_driver_gets_a_grace_period_then_loses_access()
    {
        var driver = Approved();
        driver.AddSubscription(SubscriptionKind.Paid, "Monthly", 499, 30, Now);
        var end = Now.AddDays(30);

        var grace = driver.SubscriptionAt(end.AddDays(2), Options);
        Assert.Equal(SubscriptionPhase.Grace, grace.Phase);
        Assert.True(grace.CanTakeRides);
        Assert.Equal(end.AddDays(3), grace.GraceEndsAt);

        var expired = driver.SubscriptionAt(end.AddDays(3), Options);
        Assert.Equal(SubscriptionPhase.Expired, expired.Phase);
        Assert.False(expired.CanTakeRides);
        Assert.False(driver.CanAcceptRides(end.AddDays(3), Options));
    }

    [Fact]
    public void Zero_grace_days_cuts_off_at_the_end()
    {
        var driver = Approved();
        driver.AddSubscription(SubscriptionKind.Paid, "Monthly", 499, 30, Now);

        var state = driver.SubscriptionAt(Now.AddDays(30), new SubscriptionOptions { GraceDays = 0 });

        Assert.Equal(SubscriptionPhase.Expired, state.Phase);
    }

    [Fact]
    public void Renewing_early_adds_days_after_the_current_end()
    {
        var driver = Approved();
        driver.AddSubscription(SubscriptionKind.Paid, "Monthly", 499, 30, Now);

        var renewal = driver.AddSubscription(SubscriptionKind.Paid, "Monthly", 499, 30, Now.AddDays(10));

        Assert.Equal(Now.AddDays(30), renewal.StartsAt);
        Assert.Equal(Now.AddDays(60), renewal.EndsAt);
        Assert.Equal(Now.AddDays(60), driver.SubscriptionAt(Now.AddDays(10), Options).ValidUntil);
    }

    [Fact]
    public void Renewing_after_expiry_starts_from_today()
    {
        var driver = Approved();
        driver.AddSubscription(SubscriptionKind.Paid, "Monthly", 499, 30, Now);
        var later = Now.AddDays(45);

        var renewal = driver.AddSubscription(SubscriptionKind.Granted, "Monthly", 0, 7, later, "Support case", "+919999999999");

        Assert.Equal(later, renewal.StartsAt);
        Assert.Equal(later.AddDays(7), renewal.EndsAt);
        Assert.Equal("Support case", renewal.Note);
        Assert.Equal(SubscriptionPhase.Active, driver.SubscriptionAt(later, Options).Phase);
    }

    [Fact]
    public void A_granted_period_is_not_a_trial()
    {
        var driver = Approved();
        driver.AddSubscription(SubscriptionKind.Granted, "Monthly", 0, 10, Now, "Goodwill", "+919999999999");

        Assert.False(driver.SubscriptionAt(Now, Options).IsTrial);
    }

    [Theory]
    [InlineData(SubscriptionStatus.Cancelled)]
    [InlineData(SubscriptionStatus.PaymentFailed)]
    public void Cancelled_or_failed_periods_give_no_access(SubscriptionStatus status)
    {
        var driver = Approved();
        driver.AddSubscription(SubscriptionKind.Paid, "Monthly", 499, 30, Now).Status = status;

        var state = driver.SubscriptionAt(Now.AddDays(1), Options);

        Assert.Equal(SubscriptionPhase.None, state.Phase);
        Assert.False(state.CanTakeRides);
    }

    [Fact]
    public void Switching_subscriptions_off_lets_everyone_work()
    {
        var driver = Approved();
        var off = new SubscriptionOptions { Enabled = false };

        Assert.True(driver.SubscriptionAt(Now, off).CanTakeRides);
        Assert.True(driver.CanAcceptRides(Now, off));
        Assert.False(driver.StartTrialIfNew(Now, off));
        driver.EnsureSubscribed(Now, off); // does not throw
    }

    [Fact]
    public void Unverified_driver_cannot_accept_rides_even_with_a_subscription()
    {
        var driver = Approved();
        driver.AddSubscription(SubscriptionKind.Paid, "Monthly", 499, 30, Now);
        driver.VerificationStatus = VerificationStatus.Suspended;

        Assert.False(driver.CanAcceptRides(Now, Options));
    }

    [Fact]
    public void EnsureSubscribed_explains_what_to_do()
    {
        var driver = Approved();
        var none = Assert.Throws<InvalidOperationException>(() => driver.EnsureSubscribed(Now, Options));
        Assert.Contains("don't have a subscription", none.Message);

        driver.AddSubscription(SubscriptionKind.Paid, "Monthly", 499, 30, Now);
        var expired = Assert.Throws<InvalidOperationException>(() => driver.EnsureSubscribed(Now.AddDays(40), Options));
        Assert.Contains("ended on", expired.Message);
    }

    [Fact]
    public void EnsureSubscribed_message_names_the_action_being_refused()
    {
        var driver = Approved();
        driver.AddSubscription(SubscriptionKind.Paid, "Monthly", 499, 30, Now);

        var ex = Assert.Throws<InvalidOperationException>(
            () => driver.EnsureSubscribed(Now.AddDays(40), Options, action: "accept rides"));

        Assert.EndsWith("Renew it to accept rides.", ex.Message);
    }

    [Fact]
    public void A_driver_still_in_grace_may_accept_but_one_past_grace_may_not()
    {
        var driver = Approved();
        driver.AddSubscription(SubscriptionKind.Paid, "Monthly", 499, 30, Now);

        driver.EnsureSubscribed(Now.AddDays(31), Options, "accept rides"); // grace: fine
        Assert.Throws<InvalidOperationException>(() => driver.EnsureSubscribed(Now.AddDays(34), Options, "accept rides"));
    }

    [Fact]
    public void A_subscription_must_last_at_least_a_day()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Approved().AddSubscription(SubscriptionKind.Paid, "Monthly", 499, 0, Now));
    }
}
