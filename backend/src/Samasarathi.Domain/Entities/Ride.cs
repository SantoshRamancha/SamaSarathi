using NetTopologySuite.Geometries;
using Samasarathi.Domain.Pricing;

namespace Samasarathi.Domain.Entities;

public class Ride
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RiderId { get; set; }
    public Guid? DriverId { get; set; }
    public RideStatus Status { get; set; } = RideStatus.Requested;

    public required Point Pickup { get; set; }
    public required Point Drop { get; set; }

    /// <summary>Driver's rate copied at booking, so later rate changes don't affect this ride.</summary>
    public decimal RatePerKm { get; set; }
    public PricingMode PricingMode { get; set; }

    public decimal EstimatedDistanceKm { get; set; }
    public decimal EstimatedMinutes { get; set; }
    public decimal EstimatedFare { get; set; }

    public decimal? ActualDistanceKm { get; set; }
    public decimal? ActualMinutes { get; set; }
    public decimal? FinalFare { get; set; }

    public DateTimeOffset RequestedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? AcceptedAt { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public DateTimeOffset? CancelledAt { get; set; }

    /// <summary>Shown to the other side, e.g. "Cancelled by driver".</summary>
    public string? CancelReason { get; set; }

    public const int MaxCommentLength = 300;

    // After a completed trip each side can rate the other once: 1 to 5 stars and an optional short comment.
    public int? RatingForDriver { get; set; }
    public string? CommentForDriver { get; set; }
    public DateTimeOffset? RatedDriverAt { get; set; }

    public int? RatingForRider { get; set; }
    public string? CommentForRider { get; set; }
    public DateTimeOffset? RatedRiderAt { get; set; }

    /// <summary>A rider or driver can have only one of these at a time.</summary>
    public static readonly RideStatus[] ActiveStatuses =
        [RideStatus.Requested, RideStatus.Accepted, RideStatus.DriverArrived, RideStatus.InProgress];

    public bool IsActive => ActiveStatuses.Contains(Status);

    /// <summary>The rider picked <paramref name="driver"/> from the map; the driver now has to accept or decline.</summary>
    public static Ride Request(
        Guid riderId, Driver driver, double pickupLat, double pickupLng, double dropLat, double dropLng,
        RideEstimate estimate, DateTimeOffset now)
    {
        // Geometry points are (x = longitude, y = latitude); SRID 4326 = standard GPS coordinates.
        static Point At(double lat, double lng) => new(lng, lat) { SRID = 4326 };
        return new Ride
        {
            RiderId = riderId,
            DriverId = driver.Id,
            Pickup = At(pickupLat, pickupLng),
            Drop = At(dropLat, dropLng),
            RatePerKm = driver.RatePerKm,
            PricingMode = estimate.Quote.Mode,
            EstimatedDistanceKm = estimate.DistanceKm,
            EstimatedMinutes = estimate.Minutes,
            EstimatedFare = estimate.Quote.Fare,
            RequestedAt = now,
        };
    }

    /// <summary>When the driver's chance to accept runs out.</summary>
    public DateTimeOffset OfferExpiresAt(TimeSpan timeout) => RequestedAt + timeout;

    /// <summary>
    /// A request the driver never answered is dropped, so the rider isn't left waiting and the driver isn't stuck.
    /// Called whenever a ride is read (there is no background job yet). Returns true if it changed the ride.
    /// </summary>
    public bool ExpireIfStale(DateTimeOffset now, TimeSpan timeout)
    {
        if (Status != RideStatus.Requested || now < OfferExpiresAt(timeout)) return false;
        Status = RideStatus.Expired;
        return true;
    }

    public void Accept(Guid driverId, DateTimeOffset now)
    {
        EnsureDriver(driverId);
        if (Status != RideStatus.Requested)
            throw new InvalidOperationException($"This request can no longer be accepted ({Status}).");
        Status = RideStatus.Accepted;
        AcceptedAt = now;
    }

    public void Decline(Guid driverId)
    {
        EnsureDriver(driverId);
        if (Status != RideStatus.Requested)
            throw new InvalidOperationException($"This request can no longer be declined ({Status}).");
        Status = RideStatus.Declined;
    }

    /// <summary>The rider rates the driver after the trip. Once only.</summary>
    public void RateDriver(int stars, string? comment, DateTimeOffset now)
    {
        EnsureRateable(RatingForDriver, stars);
        RatingForDriver = stars;
        CommentForDriver = CleanComment(comment);
        RatedDriverAt = now;
    }

    /// <summary>The driver rates the rider after the trip. Once only.</summary>
    public void RateRider(Guid driverId, int stars, string? comment, DateTimeOffset now)
    {
        EnsureDriver(driverId);
        EnsureRateable(RatingForRider, stars);
        RatingForRider = stars;
        CommentForRider = CleanComment(comment);
        RatedRiderAt = now;
    }

    private void EnsureRateable(int? alreadyGiven, int stars)
    {
        if (Status != RideStatus.Completed)
            throw new InvalidOperationException("You can rate a trip only after it is completed.");
        if (alreadyGiven is not null)
            throw new InvalidOperationException("You have already rated this trip.");
        if (stars is < 1 or > 5)
            throw new ArgumentOutOfRangeException(nameof(stars), "A rating is 1 to 5 stars.");
    }

    private static string? CleanComment(string? comment)
    {
        var text = comment?.Trim();
        if (string.IsNullOrEmpty(text)) return null;
        return text.Length <= MaxCommentLength ? text : text[..MaxCommentLength];
    }

    /// <summary>The driver is at the pickup point.</summary>
    public void MarkArrived(Guid driverId)
    {
        EnsureDriver(driverId);
        if (Status != RideStatus.Accepted)
            throw new InvalidOperationException($"You can only mark arrival after accepting the ride ({Status}).");
        Status = RideStatus.DriverArrived;
    }

    /// <summary>The rider is in the vehicle and the trip begins. The clock for the final fare starts here.</summary>
    public void Start(Guid driverId, DateTimeOffset now)
    {
        EnsureDriver(driverId);
        if (Status != RideStatus.DriverArrived)
            throw new InvalidOperationException("Mark that you have arrived at the pickup before starting the trip.");
        Status = RideStatus.InProgress;
        StartedAt = now;
    }

    /// <summary>
    /// Drop-off. Time taken is measured; distance is still the estimate (there is no GPS trace of the trip yet).
    /// Charged by the mode locked at booking, and a time-based fare is capped above the estimate (see FareCalculator.Final).
    /// </summary>
    public void Complete(Guid driverId, DateTimeOffset now, FareCalculator calculator)
    {
        EnsureDriver(driverId);
        if (Status != RideStatus.InProgress || StartedAt is not { } started)
            throw new InvalidOperationException("Only a trip in progress can be completed.");

        var minutes = Math.Max(0m, Math.Round((decimal)(now - started).TotalMinutes, 1));
        Status = RideStatus.Completed;
        CompletedAt = now;
        ActualMinutes = minutes;
        ActualDistanceKm = EstimatedDistanceKm;
        FinalFare = calculator.Final(RatePerKm, EstimatedDistanceKm, minutes, PricingMode, EstimatedFare).Fare;
    }

    /// <summary>Allowed before the trip starts, by the rider or the driver.</summary>
    public void Cancel(bool byDriver, DateTimeOffset now)
    {
        if (Status is not (RideStatus.Requested or RideStatus.Accepted or RideStatus.DriverArrived))
            throw new InvalidOperationException($"This ride can't be cancelled now ({Status}).");
        Status = RideStatus.Cancelled;
        CancelledAt = now;
        CancelReason = byDriver ? "Cancelled by driver" : "Cancelled by rider";
    }

    private void EnsureDriver(Guid driverId)
    {
        if (DriverId != driverId)
            throw new InvalidOperationException("This ride belongs to another driver.");
    }
}
