using Samasarathi.Application.Contracts;
using Samasarathi.Domain;

namespace Samasarathi.Admin.Components.Shared;

/// <summary>Human wording and colours for the API's enum values.</summary>
public static class Labels
{
    public static string Of(VerificationStatus s) => s switch
    {
        VerificationStatus.Pending => "Not submitted",
        VerificationStatus.InReview => "In review",
        VerificationStatus.Approved => "Approved",
        VerificationStatus.Rejected => "Changes requested",
        VerificationStatus.Suspended => "Suspended",
        _ => s.ToString(),
    };

    public static string Badge(VerificationStatus s) => s switch
    {
        VerificationStatus.InReview => "text-bg-primary",
        VerificationStatus.Approved => "text-bg-success",
        VerificationStatus.Rejected => "text-bg-warning",
        VerificationStatus.Suspended => "text-bg-danger",
        _ => "text-bg-secondary",
    };

    public static string Of(VerificationCheckType t) => t switch
    {
        VerificationCheckType.DrivingLicence => "Driving licence",
        VerificationCheckType.IdentityProof => "Identity",
        VerificationCheckType.PoliceRecord => "Police / criminal record",
        VerificationCheckType.DrivingSkillTest => "Driving skill test",
        VerificationCheckType.BehaviourAssessment => "Behaviour assessment",
        _ => t.ToString(),
    };

    /// <summary>What the reviewer should look at for each check.</summary>
    public static string Guidance(VerificationCheckType t) => t switch
    {
        VerificationCheckType.DrivingLicence => "Licence is valid, not expired, covers this vehicle class, and the name matches.",
        VerificationCheckType.IdentityProof => "ID matches the driver's name and photo.",
        VerificationCheckType.PoliceRecord => "Police verification certificate or background-check report is clear.",
        VerificationCheckType.DrivingSkillTest => "Driver passed our road test.",
        VerificationCheckType.BehaviourAssessment => "Interview: courteous, follows safety rules, no red flags.",
        _ => "",
    };

    public static string Badge(CheckResult r) => r switch
    {
        CheckResult.Passed => "text-bg-success",
        CheckResult.Failed => "text-bg-danger",
        _ => "text-bg-light border",
    };

    public static string Of(DocumentType t) => t switch
    {
        DocumentType.ProfilePhoto => "Photo",
        DocumentType.DrivingLicence => "Driving licence",
        DocumentType.VehicleRC => "Vehicle RC",
        DocumentType.IdentityProof => "ID proof",
        _ => t.ToString(),
    };

    public static string Of(RideStatus s) => s switch
    {
        RideStatus.Requested => "Waiting for driver",
        RideStatus.Accepted => "Driver on the way",
        RideStatus.DriverArrived => "Driver at pickup",
        RideStatus.InProgress => "In progress",
        RideStatus.Completed => "Completed",
        RideStatus.Cancelled => "Cancelled",
        RideStatus.Declined => "Declined",
        RideStatus.Expired => "Not answered",
        _ => s.ToString(),
    };

    public static string Badge(RideStatus s) => s switch
    {
        RideStatus.Requested or RideStatus.Accepted or RideStatus.DriverArrived or RideStatus.InProgress => "text-bg-primary",
        RideStatus.Completed => "text-bg-success",
        RideStatus.Cancelled => "text-bg-warning",
        _ => "text-bg-secondary",
    };

    public static string Of(AdminRideFilter f) => f switch
    {
        AdminRideFilter.Active => "Active now",
        AdminRideFilter.Completed => "Completed",
        AdminRideFilter.Unsuccessful => "Not completed",
        _ => "All",
    };

    public static string Of(AdminSubscriptionFilter f) => f switch
    {
        AdminSubscriptionFilter.ExpiringSoon => "Expiring soon",
        AdminSubscriptionFilter.Active => "Active",
        AdminSubscriptionFilter.Trial => "On free trial",
        AdminSubscriptionFilter.Grace => "In grace",
        AdminSubscriptionFilter.Expired => "Expired",
        AdminSubscriptionFilter.None => "Not started",
        _ => "All",
    };

    public static string Of(SubscriptionPhase p, bool isTrial) => p switch
    {
        SubscriptionPhase.Active => isTrial ? "Free trial" : "Active",
        SubscriptionPhase.Grace => "In grace",
        SubscriptionPhase.Expired => "Expired",
        _ => "Not started",
    };

    public static string Badge(SubscriptionPhase p) => p switch
    {
        SubscriptionPhase.Active => "text-bg-success",
        SubscriptionPhase.Grace => "text-bg-warning",
        SubscriptionPhase.Expired => "text-bg-danger",
        _ => "text-bg-secondary",
    };

    /// <summary>"★★★★☆" for a 1 to 5 rating; a dash when nobody has rated.</summary>
    public static string Stars(int? rating) =>
        rating is { } r and >= 1 and <= 5 ? new string('★', r) + new string('☆', 5 - r) : "—";

    public static string Size(long bytes) =>
        bytes < 1024 ? "under 1 KB"
        : bytes < 1024 * 1024 ? $"{bytes / 1024} KB"
        : $"{bytes / (1024.0 * 1024):0.0} MB";

    public static string When(DateTimeOffset? at) =>
        at is { } t ? t.ToLocalTime().ToString("d MMM yyyy, h:mm tt") : "—";

    /// <summary>"3 days", "5 hours": how long a driver has been waiting.</summary>
    public static string Ago(DateTimeOffset? at, DateTimeOffset now)
    {
        if (at is not { } t) return "—";
        var span = now - t;
        return span.TotalDays >= 1 ? $"{(int)span.TotalDays} d"
            : span.TotalHours >= 1 ? $"{(int)span.TotalHours} h"
            : $"{Math.Max(1, (int)span.TotalMinutes)} min";
    }
}
