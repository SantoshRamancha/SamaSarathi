using Samasarathi.Domain;
using Samasarathi.Domain.Entities;

namespace Samasarathi.Tests;

public class PhoneNumberTests
{
    [Theory]
    [InlineData("9876543210")]
    [InlineData("98765 43210")]
    [InlineData("+91 98765-43210")]
    [InlineData("919876543210")]
    [InlineData("09876543210")]
    public void Normalizes_common_formats(string input)
    {
        Assert.True(PhoneNumber.TryNormalize(input, out var phone));
        Assert.Equal("+919876543210", phone);
    }

    [Theory]
    [InlineData("")]
    [InlineData("12345")]
    [InlineData("5876543210")]      // Indian mobiles start with 6-9
    [InlineData("98765432101")]     // 11 digits without leading 0
    [InlineData("98765abc10")]
    public void Rejects_invalid(string input) => Assert.False(PhoneNumber.TryNormalize(input, out _));
}

public class VehicleNumberTests
{
    [Theory]
    [InlineData("ka 01 ab-1234", "KA01AB1234")]
    [InlineData("TS09E1234", "TS09E1234")]
    [InlineData("22 BH 1234 AA", "22BH1234AA")]
    public void Normalizes_valid(string input, string expected)
    {
        Assert.True(VehicleNumber.TryNormalize(input, out var number));
        Assert.Equal(expected, number);
    }

    [Theory]
    [InlineData("")]
    [InlineData("1234")]
    [InlineData("KARNATAKA")]
    public void Rejects_invalid(string input) => Assert.False(VehicleNumber.TryNormalize(input, out _));
}

public class DriverOnboardingTests
{
    private static Driver CompleteDriver()
    {
        var driver = new Driver { FullName = "Ravi Kumar", Phone = "+919876543210", RatePerKm = 10 };
        driver.Vehicle = new Vehicle { DriverId = driver.Id, Type = VehicleType.Bike, RegistrationNumber = "KA01AB1234" };
        foreach (var type in Driver.RequiredDocuments)
            driver.Documents.Add(new DriverDocument
            {
                Type = type, StorageKey = $"{type}.jpg", OriginalFileName = "x.jpg", ContentType = "image/jpeg"
            });
        return driver;
    }

    [Fact]
    public void New_driver_is_missing_vehicle_documents_and_rate()
    {
        var driver = new Driver { FullName = "Ravi", Phone = "+919876543210" };

        Assert.Equal([OnboardingStep.Vehicle, OnboardingStep.Documents, OnboardingStep.Rate], driver.MissingSteps());
    }

    [Fact]
    public void One_missing_document_keeps_documents_step_open()
    {
        var driver = CompleteDriver();
        driver.Documents.RemoveAll(d => d.Type == DocumentType.VehicleRC);

        Assert.Equal([OnboardingStep.Documents], driver.MissingSteps());
    }

    [Fact]
    public void Cannot_submit_incomplete_registration()
    {
        var driver = CompleteDriver();
        driver.RatePerKm = 0;

        var ex = Assert.Throws<InvalidOperationException>(() => driver.SubmitForVerification(DateTimeOffset.UtcNow));
        Assert.Contains("Rate", ex.Message);
        Assert.Equal(VerificationStatus.Pending, driver.VerificationStatus);
    }

    [Fact]
    public void Submitting_starts_review_and_opens_every_check()
    {
        var driver = CompleteDriver();

        driver.SubmitForVerification(DateTimeOffset.UtcNow);

        Assert.Equal(VerificationStatus.InReview, driver.VerificationStatus);
        Assert.False(driver.CanEditOnboarding);
        Assert.Equal(Enum.GetValues<VerificationCheckType>().Length, driver.VerificationChecks.Count);
        Assert.All(driver.VerificationChecks, c => Assert.Equal(CheckResult.Pending, c.Result));
    }

    [Fact]
    public void Cannot_submit_twice()
    {
        var driver = CompleteDriver();
        driver.SubmitForVerification(DateTimeOffset.UtcNow);

        Assert.Throws<InvalidOperationException>(() => driver.SubmitForVerification(DateTimeOffset.UtcNow));
    }
}
