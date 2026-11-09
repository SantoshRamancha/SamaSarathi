using System.ComponentModel.DataAnnotations;
using Samasarathi.Application.Abstractions;
using Samasarathi.Domain;
using Samasarathi.Domain.Entities;

namespace Samasarathi.Api.Controllers;

/// <summary>Average stars and how many ratings it is based on. <see cref="Average"/> is null until the first rating.</summary>
public sealed record RatingSummaryDto(double? Average, int Count)
{
    public static RatingSummaryDto None { get; } = new(null, 0);
}

public sealed record RatingRequest([Range(1, 5)] int Stars, [StringLength(Ride.MaxCommentLength)] string? Comment);

/// <summary>The driver, as the rider sees them. The phone is shared only once the driver has accepted.</summary>
public sealed record RideDriverDto(
    Guid Id, string Name, VehicleType VehicleType, string RegistrationNumber, string? Model, string? Phone,
    double? Latitude, double? Longitude, double? RatingAverage, int RatingCount);

/// <summary>A ride as the rider sees it.</summary>
/// <param name="MyRating">The stars the rider gave the driver, once they have rated.</param>
public sealed record RideDto(
    Guid Id, RideStatus Status,
    double PickupLatitude, double PickupLongitude, double DropLatitude, double DropLongitude,
    decimal EstimatedDistanceKm, decimal EstimatedMinutes, decimal EstimatedFare, PricingMode PricingMode, decimal RatePerKm,
    DateTimeOffset RequestedAt, DateTimeOffset? ExpiresAt, string? CancelReason, RideDriverDto? Driver,
    decimal? ActualMinutes, decimal? FinalFare, int? MyRating, string? MyComment)
{
    public static RideDto From(Ride r, Driver? driver, TimeSpan offerTimeout, RatingSummaryDto? driverRating = null) => new(
        r.Id, r.Status,
        r.Pickup.Y, r.Pickup.X, r.Drop.Y, r.Drop.X,
        r.EstimatedDistanceKm, r.EstimatedMinutes, r.EstimatedFare, r.PricingMode, r.RatePerKm,
        r.RequestedAt,
        r.Status == RideStatus.Requested ? r.OfferExpiresAt(offerTimeout) : null,
        r.CancelReason,
        driver is null || driver.Vehicle is null ? null : new RideDriverDto(
            driver.Id, driver.FullName, driver.Vehicle.Type, driver.Vehicle.RegistrationNumber, driver.Vehicle.Model,
            r.Status == RideStatus.Requested ? null : driver.Phone,
            driver.LastKnownLocation?.Y, driver.LastKnownLocation?.X,
            driverRating?.Average, driverRating?.Count ?? 0),
        r.ActualMinutes, r.FinalFare, r.RatingForDriver, r.CommentForDriver);
}

/// <summary>A ride as the driver sees it. The rider's phone is shared only after the driver accepts.</summary>
/// <param name="YourRating">The stars the driver gave the rider, once they have rated.</param>
public sealed record DriverRideDto(
    Guid Id, RideStatus Status,
    double PickupLatitude, double PickupLongitude, double DropLatitude, double DropLongitude,
    decimal EstimatedDistanceKm, decimal EstimatedMinutes, decimal EstimatedFare,
    double? PickupDistanceKm, DateTimeOffset RequestedAt, DateTimeOffset? ExpiresAt,
    string RiderName, string? RiderPhone, decimal? ActualMinutes, decimal? FinalFare, PricingMode PricingMode,
    int? YourRating)
{
    public static DriverRideDto From(Ride r, Driver driver, Rider rider, TimeSpan offerTimeout) => new(
        r.Id, r.Status,
        r.Pickup.Y, r.Pickup.X, r.Drop.Y, r.Drop.X,
        r.EstimatedDistanceKm, r.EstimatedMinutes, r.EstimatedFare,
        driver.LastKnownLocation is { } here
            ? Math.Round(GeoMath.DistanceKm(here.Y, here.X, r.Pickup.Y, r.Pickup.X), 1)
            : null,
        r.RequestedAt,
        r.Status == RideStatus.Requested ? r.OfferExpiresAt(offerTimeout) : null,
        rider.FullName,
        r.Status == RideStatus.Requested ? null : rider.Phone,
        r.ActualMinutes, r.FinalFare, r.PricingMode, r.RatingForRider);
}

/// <summary>The road lines to draw for a ride, as [latitude, longitude] points.</summary>
/// <param name="Trip">Pickup to drop-off.</param>
/// <param name="ToPickup">Driver's position to the pickup, only while the driver is on the way to it.</param>
public sealed record RideRouteDto(IReadOnlyList<double[]> Trip, IReadOnlyList<double[]> ToPickup)
{
    public static double[] Point(GeoPoint p) => [p.Latitude, p.Longitude];
}

/// <summary>
/// The road the driver should follow next, as [latitude, longitude] points. Empty when there is nothing to navigate or routing is down.
/// The driver app reads this as <c>{ "route": [[lat, lng], ...] }</c>.
/// </summary>
public sealed record DriverRouteDto(IReadOnlyList<double[]> Route);

/// <summary>One past ride in a person's history. "Other" is the driver for a rider, and the rider for a driver.</summary>
/// <param name="Fare">What was charged (the final fare), or the estimate if the trip never finished.</param>
/// <param name="GivenRating">Stars this person gave the other side.</param>
/// <param name="ReceivedRating">Stars the other side gave this person.</param>
/// <param name="CanRate">The trip is completed and this person has not rated yet.</param>
public sealed record RideHistoryItem(
    Guid Id, RideStatus Status, DateTimeOffset RequestedAt, DateTimeOffset? EndedAt,
    decimal DistanceKm, decimal Fare, PricingMode PricingMode, string OtherName, string? OtherDetail,
    int? GivenRating, int? ReceivedRating, bool CanRate, string? CancelReason);
