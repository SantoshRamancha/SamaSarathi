using System.ComponentModel.DataAnnotations;
using Samasarathi.Domain;

namespace Samasarathi.Application.Contracts;

// Request/response shapes of the /api/admin endpoints. They live here, not in the Api project,
// so the admin portal can use the exact same types when it calls the API.

public sealed record AdminDriverCounts(int Pending, int InReview, int Approved, int Rejected, int Suspended);

public sealed record AdminDriverSummary(
    Guid Id, string FullName, string Phone, VehicleType? VehicleType, string? VehicleNumber,
    decimal RatePerKm, VerificationStatus Status, DateTimeOffset CreatedAt, DateTimeOffset? SubmittedAt,
    int ChecksPassed, int ChecksFailed, int ChecksTotal);

public sealed record AdminDocument(DocumentType Type, string FileName, string ContentType, long SizeBytes, DateTimeOffset UploadedAt);

public sealed record AdminCheck(
    VerificationCheckType Type, CheckResult Result, string? Notes, string? CheckedBy,
    DateTimeOffset? CheckedAt, DateTimeOffset CreatedAt);

/// <summary>One rating a rider gave this driver, shown to our team when deciding whether to suspend.</summary>
public sealed record AdminDriverRating(int Stars, string? Comment, DateTimeOffset RatedAt);

/// <param name="Checks">Checks for the current submission.</param>
/// <param name="History">Checks from earlier submissions, newest first.</param>
/// <param name="RatingAverage">Average stars from riders; null until the first rating.</param>
/// <param name="RecentRatings">The latest ratings (up to 10), newest first.</param>
public sealed record AdminDriverDetail(
    Guid Id, string FullName, string Phone, decimal RatePerKm, VerificationStatus Status,
    DateTimeOffset CreatedAt, DateTimeOffset? SubmittedAt, DateTimeOffset? ReviewedAt, string? ReviewedBy,
    string? RejectionReason, VehicleType? VehicleType, string? VehicleNumber, string? VehicleModel,
    IReadOnlyList<AdminDocument> Documents, IReadOnlyList<AdminCheck> Checks, IReadOnlyList<AdminCheck> History,
    double? RatingAverage, int RatingCount, IReadOnlyList<AdminDriverRating> RecentRatings);

public sealed record RecordCheckRequest(CheckResult Result, [StringLength(500)] string? Notes);

public sealed record RejectRequest([Required, StringLength(500, MinimumLength = 5)] string Reason);

/// <summary>The reason is shown to the driver, so it should say what they need to do.</summary>
public sealed record SuspendRequest([Required, StringLength(500, MinimumLength = 5)] string Reason);

// ---- Subscriptions ----

/// <summary>Which approved drivers to list. ExpiringSoon = paid up but within the warning window, or already in grace.</summary>
public enum AdminSubscriptionFilter { ExpiringSoon, Active, Trial, Grace, Expired, None, All }

/// <param name="None">Approved drivers who have not opened the app since approval, so have no subscription yet.</param>
public sealed record AdminSubscriptionCounts(int Active, int Trial, int ExpiringSoon, int Grace, int Expired, int None);

public sealed record AdminSubscriptionSummary(
    Guid DriverId, string FullName, string Phone, string? VehicleNumber, SubscriptionPhase Phase, bool IsTrial,
    DateTimeOffset? ValidUntil, int? DaysLeft, bool ExpiringSoon, SubscriptionKind? LastKind, string? LastNote);

/// <summary>The business numbers currently in force (read-only here; they are set in the API's "Subscription" config).</summary>
public sealed record AdminSubscriptionSettings(
    bool Enabled, string PlanName, decimal MonthlyPrice, int PlanDays, int TrialDays, int GraceDays, int ExpiryWarningDays,
    bool PaymentsEnabled);

public sealed record GrantSubscriptionRequest(
    [Range(1, 366)] int Days, [Required, StringLength(200, MinimumLength = 3)] string Note);

// ---- Rides (read-only view for our team) ----

public enum AdminRideFilter { Active, Completed, Unsuccessful, All }

/// <param name="Unsuccessful24h">Cancelled, declined or expired requests in the last 24 hours.</param>
public sealed record AdminRideCounts(int Active, int Completed24h, int Unsuccessful24h, int Total);

public sealed record AdminRideSummary(
    Guid Id, RideStatus Status, DateTimeOffset RequestedAt,
    string RiderName, string RiderPhone, string? DriverName, string? DriverPhone, string? VehicleNumber,
    decimal DistanceKm, decimal Fare, PricingMode PricingMode, int? RatingForDriver, int? RatingForRider);

public sealed record AdminRideDetail(
    Guid Id, RideStatus Status, PricingMode PricingMode,
    string RiderName, string RiderPhone,
    Guid? DriverId, string? DriverName, string? DriverPhone, VehicleType? VehicleType, string? VehicleNumber,
    double PickupLatitude, double PickupLongitude, double DropLatitude, double DropLongitude,
    decimal RatePerKm, decimal EstimatedDistanceKm, decimal EstimatedMinutes, decimal EstimatedFare,
    decimal? ActualMinutes, decimal? FinalFare,
    DateTimeOffset RequestedAt, DateTimeOffset? AcceptedAt, DateTimeOffset? StartedAt, DateTimeOffset? CompletedAt,
    DateTimeOffset? CancelledAt, string? CancelReason,
    int? RatingForDriver, string? CommentForDriver, int? RatingForRider, string? CommentForRider);
