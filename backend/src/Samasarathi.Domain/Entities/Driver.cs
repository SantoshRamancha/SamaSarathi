using NetTopologySuite.Geometries;

namespace Samasarathi.Domain.Entities;

public class Driver
{
    public const decimal MinRatePerKm = 3m;
    public const decimal MaxRatePerKm = 100m;

    public static readonly DocumentType[] RequiredDocuments =
        [DocumentType.ProfilePhoto, DocumentType.DrivingLicence, DocumentType.VehicleRC, DocumentType.IdentityProof];

    public Guid Id { get; set; } = Guid.NewGuid();
    public required string FullName { get; set; }
    public required string Phone { get; set; }

    /// <summary>Driver's own rate in normal traffic, set at registration (INR per km).</summary>
    public decimal RatePerKm { get; set; }

    public VerificationStatus VerificationStatus { get; set; } = VerificationStatus.Pending;
    public DateTimeOffset? SubmittedAt { get; set; }

    /// <summary>Last approve/reject decision by our team.</summary>
    public DateTimeOffset? ReviewedAt { get; set; }
    public string? ReviewedBy { get; set; }

    /// <summary>Shown to the driver so they know what to fix before resubmitting.</summary>
    public string? RejectionReason { get; set; }
    public bool IsOnline { get; set; }
    public Point? LastKnownLocation { get; set; }
    public DateTimeOffset? LastLocationAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Vehicle? Vehicle { get; set; }
    public List<DriverDocument> Documents { get; set; } = [];
    public List<Subscription> Subscriptions { get; set; } = [];
    public List<VerificationCheck> VerificationChecks { get; set; } = [];

    /// <summary>Details are locked while our team reviews them, and after approval.</summary>
    public bool CanEditOnboarding =>
        VerificationStatus is VerificationStatus.Pending or VerificationStatus.Rejected;

    public IReadOnlyList<OnboardingStep> MissingSteps()
    {
        var missing = new List<OnboardingStep>();
        if (string.IsNullOrWhiteSpace(FullName)) missing.Add(OnboardingStep.Profile);
        if (Vehicle is null) missing.Add(OnboardingStep.Vehicle);
        if (RequiredDocuments.Any(t => Documents.All(d => d.Type != t))) missing.Add(OnboardingStep.Documents);
        if (RatePerKm <= 0) missing.Add(OnboardingStep.Rate);
        return missing;
    }

    /// <summary>Hands the driver to our verification team and opens one pending check per verification type.</summary>
    public void SubmitForVerification(DateTimeOffset now)
    {
        if (!CanEditOnboarding)
            throw new InvalidOperationException("Your details are already submitted.");

        var missing = MissingSteps();
        if (missing.Count > 0)
            throw new InvalidOperationException($"Complete these steps first: {string.Join(", ", missing)}.");

        VerificationStatus = VerificationStatus.InReview;
        SubmittedAt = now;
        RejectionReason = null;
        // A resubmission may come with new documents, so every finished check (passed or failed) starts again.
        // Checks still pending from the last round simply carry on.
        foreach (var type in Enum.GetValues<VerificationCheckType>())
        {
            if (!VerificationChecks.Any(c => c.Type == type && c.Result == CheckResult.Pending))
                VerificationChecks.Add(new VerificationCheck { DriverId = Id, Type = type, CreatedAt = now });
        }
    }

    /// <summary>The latest check of each type: the ones that count for this submission.</summary>
    public IReadOnlyList<VerificationCheck> CurrentChecks() =>
        VerificationChecks
            .GroupBy(c => c.Type)
            .Select(g => g.MaxBy(c => c.CreatedAt)!)
            .OrderBy(c => c.Type)
            .ToList();

    /// <summary>A reviewer records the outcome of one check. A failed check needs a note the driver can act on.</summary>
    public void RecordCheck(VerificationCheckType type, CheckResult result, string? notes, string reviewer, DateTimeOffset now)
    {
        EnsureInReview();
        if (result == CheckResult.Failed && string.IsNullOrWhiteSpace(notes))
            throw new InvalidOperationException("Add a note explaining why the check failed.");

        var check = CurrentChecks().First(c => c.Type == type);
        check.Result = result;
        check.Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        check.Provider = reviewer;
        check.CheckedAt = result == CheckResult.Pending ? null : now;
    }

    /// <summary>Only a driver who passed every check may be approved.</summary>
    public void Approve(string reviewer, DateTimeOffset now)
    {
        EnsureInReview();
        var notPassed = CurrentChecks().Where(c => c.Result != CheckResult.Passed).Select(c => c.Type).ToList();
        if (notPassed.Count > 0)
            throw new InvalidOperationException($"These checks have not passed yet: {string.Join(", ", notPassed)}.");

        VerificationStatus = VerificationStatus.Approved;
        RejectionReason = null;
        ReviewedAt = now;
        ReviewedBy = reviewer;
    }

    /// <summary>Sends the registration back to the driver, who can fix it and submit again.</summary>
    public void Reject(string reason, string reviewer, DateTimeOffset now)
    {
        EnsureInReview();
        if (string.IsNullOrWhiteSpace(reason))
            throw new InvalidOperationException("Tell the driver why, so they can fix it.");

        VerificationStatus = VerificationStatus.Rejected;
        RejectionReason = reason.Trim();
        ReviewedAt = now;
        ReviewedBy = reviewer;
    }

    /// <summary>
    /// Starts taking ride requests from where the driver is now. Only verified drivers may go online.
    /// The subscription rule (<see cref="EnsureSubscribed"/>) is checked by the API before calling this.
    /// </summary>
    public void GoOnline(double latitude, double longitude, DateTimeOffset now)
    {
        if (VerificationStatus != VerificationStatus.Approved)
            throw new InvalidOperationException("Only verified drivers can go online.");

        IsOnline = true;
        UpdateLocation(latitude, longitude, now);
    }

    public void GoOffline() => IsOnline = false;

    /// <summary>The driver app reports its position every few seconds while online.</summary>
    public void UpdateLocation(double latitude, double longitude, DateTimeOffset now)
    {
        if (!IsOnline)
            throw new InvalidOperationException("Go online before sharing your location.");
        if (latitude is < -90 or > 90 || longitude is < -180 or > 180 || double.IsNaN(latitude) || double.IsNaN(longitude))
            throw new ArgumentOutOfRangeException(nameof(latitude), "Location is not a valid latitude/longitude.");

        // GPS order is (latitude, longitude), but geometry points are (x = longitude, y = latitude).
        // SRID 4326 = the standard GPS coordinate system (WGS 84).
        LastKnownLocation = new Point(longitude, latitude) { SRID = 4326 };
        LastLocationAt = now;
    }

    private void EnsureInReview()
    {
        if (VerificationStatus != VerificationStatus.InReview)
            throw new InvalidOperationException($"This driver is {VerificationStatus}, not waiting for review.");
    }

    /// <summary>What this driver's subscription periods allow right now. Load <see cref="Subscriptions"/> first.</summary>
    public SubscriptionState SubscriptionAt(DateTimeOffset now, SubscriptionOptions options) =>
        SubscriptionPolicy.Evaluate(Subscriptions, now, options);

    /// <summary>A driver may take rides only when fully verified and paid up (or in the short grace period).</summary>
    public bool CanAcceptRides(DateTimeOffset now, SubscriptionOptions options) =>
        VerificationStatus == VerificationStatus.Approved && SubscriptionAt(now, options).CanTakeRides;

    /// <summary>Throws with a message the driver can act on when the subscription doesn't allow taking rides.</summary>
    /// <param name="action">What the driver was trying to do, completing "Renew it to ...".</param>
    public void EnsureSubscribed(DateTimeOffset now, SubscriptionOptions options, string action = "go online")
    {
        var state = SubscriptionAt(now, options);
        if (state.CanTakeRides) return;

        throw new InvalidOperationException(state.Phase == SubscriptionPhase.None
            ? "You don't have a subscription yet. Contact support to activate one."
            : $"Your subscription ended on {state.ValidUntil:d MMM yyyy}. Renew it to {action}.");
    }

    /// <summary>
    /// Adds a period of <paramref name="days"/>. If the driver is still covered it starts when the current one ends,
    /// so renewing early never wastes days.
    /// </summary>
    public Subscription AddSubscription(
        SubscriptionKind kind, string planName, decimal price, int days, DateTimeOffset now,
        string? note = null, string? grantedBy = null, string? paymentReference = null)
    {
        if (days < 1) throw new ArgumentOutOfRangeException(nameof(days), "A subscription must be at least one day.");

        var coveredUntil = Subscriptions
            .Where(s => s.Status == SubscriptionStatus.Active && s.EndsAt > now)
            .Select(s => (DateTimeOffset?)s.EndsAt).Max();
        var start = coveredUntil ?? now;

        var period = new Subscription
        {
            DriverId = Id, PlanName = planName, MonthlyPrice = price, Kind = kind,
            StartsAt = start, EndsAt = start.AddDays(days), Note = note, GrantedBy = grantedBy, PaymentReference = paymentReference,
        };
        Subscriptions.Add(period);
        return period;
    }

    /// <summary>
    /// Gives an approved driver who has never had a subscription their free trial. Called the first time they open the app
    /// after approval, so no trial days are lost while they wait. Returns false when nothing was added.
    /// </summary>
    public bool StartTrialIfNew(DateTimeOffset now, SubscriptionOptions options)
    {
        if (!options.Enabled || options.TrialDays < 1) return false;
        if (VerificationStatus != VerificationStatus.Approved || Subscriptions.Count > 0) return false;

        AddSubscription(SubscriptionKind.Trial, "Free trial", 0m, options.TrialDays, now);
        return true;
    }
}
