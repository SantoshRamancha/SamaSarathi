using Samasarathi.Domain;
using Samasarathi.Domain.Entities;

namespace Samasarathi.Tests;

public class DriverOnlineTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 2, 9, 0, 0, TimeSpan.Zero);

    // Hyderabad, Charminar
    private const double Lat = 17.3616;
    private const double Lng = 78.4747;

    private static Driver Driver(VerificationStatus status) =>
        new() { FullName = "Ravi", Phone = "+919876543210", RatePerKm = 10, VerificationStatus = status };

    [Fact]
    public void Approved_driver_goes_online_at_their_location()
    {
        var driver = Driver(VerificationStatus.Approved);

        driver.GoOnline(Lat, Lng, Now);

        Assert.True(driver.IsOnline);
        Assert.Equal(Lat, driver.LastKnownLocation!.Y);
        Assert.Equal(Lng, driver.LastKnownLocation.X);
        Assert.Equal(4326, driver.LastKnownLocation.SRID);
        Assert.Equal(Now, driver.LastLocationAt);
    }

    [Theory]
    [InlineData(VerificationStatus.Pending)]
    [InlineData(VerificationStatus.InReview)]
    [InlineData(VerificationStatus.Rejected)]
    [InlineData(VerificationStatus.Suspended)]
    public void Unverified_driver_cannot_go_online(VerificationStatus status)
    {
        var driver = Driver(status);

        Assert.Throws<InvalidOperationException>(() => driver.GoOnline(Lat, Lng, Now));
        Assert.False(driver.IsOnline);
    }

    [Fact]
    public void Offline_driver_cannot_share_location()
    {
        var driver = Driver(VerificationStatus.Approved);

        Assert.Throws<InvalidOperationException>(() => driver.UpdateLocation(Lat, Lng, Now));
    }

    [Fact]
    public void Location_updates_move_the_driver()
    {
        var driver = Driver(VerificationStatus.Approved);
        driver.GoOnline(Lat, Lng, Now);

        driver.UpdateLocation(17.4401, 78.3489, Now.AddSeconds(10));

        Assert.Equal(17.4401, driver.LastKnownLocation!.Y);
        Assert.Equal(Now.AddSeconds(10), driver.LastLocationAt);
    }

    [Theory]
    [InlineData(91, 78)]
    [InlineData(17, 181)]
    [InlineData(double.NaN, 78)]
    public void Rejects_impossible_coordinates(double lat, double lng)
    {
        var driver = Driver(VerificationStatus.Approved);
        driver.GoOnline(Lat, Lng, Now);

        Assert.Throws<ArgumentOutOfRangeException>(() => driver.UpdateLocation(lat, lng, Now));
    }

    [Fact]
    public void Going_offline_stops_location_sharing()
    {
        var driver = Driver(VerificationStatus.Approved);
        driver.GoOnline(Lat, Lng, Now);

        driver.GoOffline();

        Assert.False(driver.IsOnline);
        Assert.Throws<InvalidOperationException>(() => driver.UpdateLocation(Lat, Lng, Now));
    }
}
