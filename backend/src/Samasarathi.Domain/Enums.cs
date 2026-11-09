namespace Samasarathi.Domain;

public enum VehicleType { Bike, Auto, Car }

public enum VerificationStatus { Pending, InReview, Approved, Rejected, Suspended }

public enum VerificationCheckType
{
    DrivingLicence,
    IdentityProof,
    PoliceRecord,
    DrivingSkillTest,
    BehaviourAssessment
}

public enum CheckResult { Pending, Passed, Failed }

public enum DocumentType { ProfilePhoto, DrivingLicence, VehicleRC, IdentityProof }

/// <summary>Driver registration steps, in the order the app shows them.</summary>
public enum OnboardingStep { Profile, Vehicle, Documents, Rate }

public enum SubscriptionStatus { Active, Expired, Cancelled, PaymentFailed }

/// <summary>Where a subscription period came from.</summary>
public enum SubscriptionKind { Trial, Paid, Granted }

/// <summary>
/// None: never had one. Active: paid up. Grace: just lapsed but still allowed for a few days. Expired: no access.
/// </summary>
public enum SubscriptionPhase { None, Active, Grace, Expired }

/// <summary>
/// Requested → Accepted → DriverArrived → InProgress → Completed. A request can also end as Declined (driver said no),
/// Expired (driver didn't answer in time) or Cancelled (rider or driver backed out after requesting).
/// </summary>
public enum RideStatus { Requested, Accepted, DriverArrived, InProgress, Completed, Cancelled, Declined, Expired }

/// <summary>How a ride is charged. Locked when the ride is booked so the rider knows upfront.</summary>
public enum PricingMode { Distance, Time }
