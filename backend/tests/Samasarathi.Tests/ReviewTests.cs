using Samasarathi.Domain;
using Samasarathi.Domain.Entities;

namespace Samasarathi.Tests;

public class DriverReviewTests
{
    private const string Reviewer = "+919999999999";
    private static readonly DateTimeOffset Day1 = new(2026, 11, 1, 10, 0, 0, TimeSpan.Zero);

    private static Driver SubmittedDriver()
    {
        var driver = new Driver { FullName = "Ravi Kumar", Phone = "+919876543210", RatePerKm = 10 };
        driver.Vehicle = new Vehicle { DriverId = driver.Id, Type = VehicleType.Bike, RegistrationNumber = "KA01AB1234" };
        foreach (var type in Driver.RequiredDocuments)
            driver.Documents.Add(new DriverDocument
            {
                Type = type, StorageKey = $"{type}.jpg", OriginalFileName = "x.jpg", ContentType = "image/jpeg"
            });
        driver.SubmitForVerification(Day1);
        return driver;
    }

    private static void PassAll(Driver driver)
    {
        foreach (var type in Enum.GetValues<VerificationCheckType>())
            driver.RecordCheck(type, CheckResult.Passed, null, Reviewer, Day1);
    }

    [Fact]
    public void Approves_when_every_check_passed()
    {
        var driver = SubmittedDriver();
        PassAll(driver);

        driver.Approve(Reviewer, Day1);

        Assert.Equal(VerificationStatus.Approved, driver.VerificationStatus);
        Assert.Equal(Reviewer, driver.ReviewedBy);
        Assert.False(driver.CanEditOnboarding);
    }

    [Fact]
    public void Cannot_approve_with_a_pending_check()
    {
        var driver = SubmittedDriver();
        driver.RecordCheck(VerificationCheckType.DrivingLicence, CheckResult.Passed, null, Reviewer, Day1);

        var ex = Assert.Throws<InvalidOperationException>(() => driver.Approve(Reviewer, Day1));
        Assert.Contains(nameof(VerificationCheckType.PoliceRecord), ex.Message);
        Assert.Equal(VerificationStatus.InReview, driver.VerificationStatus);
    }

    [Fact]
    public void Failed_check_needs_a_note()
    {
        var driver = SubmittedDriver();

        Assert.Throws<InvalidOperationException>(() =>
            driver.RecordCheck(VerificationCheckType.DrivingLicence, CheckResult.Failed, "  ", Reviewer, Day1));
    }

    [Fact]
    public void Recording_a_check_stores_who_and_when()
    {
        var driver = SubmittedDriver();

        driver.RecordCheck(VerificationCheckType.DrivingLicence, CheckResult.Failed, "Licence photo is blurry", Reviewer, Day1);

        var check = driver.CurrentChecks().Single(c => c.Type == VerificationCheckType.DrivingLicence);
        Assert.Equal(CheckResult.Failed, check.Result);
        Assert.Equal("Licence photo is blurry", check.Notes);
        Assert.Equal(Reviewer, check.Provider);
        Assert.Equal(Day1, check.CheckedAt);
    }

    [Fact]
    public void Reject_needs_a_reason_and_unlocks_editing()
    {
        var driver = SubmittedDriver();
        Assert.Throws<InvalidOperationException>(() => driver.Reject("", Reviewer, Day1));

        driver.Reject("Upload a clearer licence photo", Reviewer, Day1);

        Assert.Equal(VerificationStatus.Rejected, driver.VerificationStatus);
        Assert.Equal("Upload a clearer licence photo", driver.RejectionReason);
        Assert.True(driver.CanEditOnboarding);
    }

    [Fact]
    public void Resubmitting_after_rejection_opens_fresh_checks_and_keeps_history()
    {
        var driver = SubmittedDriver();
        PassAll(driver);
        driver.Reject("Vehicle RC name doesn't match", Reviewer, Day1);

        driver.SubmitForVerification(Day1.AddDays(1));

        Assert.Equal(VerificationStatus.InReview, driver.VerificationStatus);
        Assert.Null(driver.RejectionReason);
        Assert.All(driver.CurrentChecks(), c => Assert.Equal(CheckResult.Pending, c.Result));
        Assert.Equal(Enum.GetValues<VerificationCheckType>().Length, driver.CurrentChecks().Count);
        Assert.Equal(2 * Enum.GetValues<VerificationCheckType>().Length, driver.VerificationChecks.Count);
    }

    [Fact]
    public void Cannot_review_a_driver_who_has_not_submitted()
    {
        var driver = new Driver { FullName = "Ravi", Phone = "+919876543210" };

        Assert.Throws<InvalidOperationException>(() => driver.Reject("No", Reviewer, Day1));
        Assert.Throws<InvalidOperationException>(() => driver.Approve(Reviewer, Day1));
    }

    [Fact]
    public void Cannot_review_twice()
    {
        var driver = SubmittedDriver();
        PassAll(driver);
        driver.Approve(Reviewer, Day1);

        Assert.Throws<InvalidOperationException>(() => driver.Reject("Changed my mind", Reviewer, Day1));
    }
}
