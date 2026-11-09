using Samasarathi.Domain.Entities;

namespace Samasarathi.Domain;

/// <summary>
/// "Subscription" config section. Every business number lives here so it can be changed in appsettings
/// (or an environment variable such as <c>Subscription__MonthlyPrice</c>) without touching code.
/// </summary>
public sealed class SubscriptionOptions
{
    /// <summary>Master switch. When false nobody needs a subscription (useful in development).</summary>
    public bool Enabled { get; init; } = true;

    public string PlanName { get; init; } = "Monthly";

    /// <summary>What a driver pays for one period, in INR.</summary>
    public decimal MonthlyPrice { get; init; } = 499m;

    /// <summary>Length of one paid period.</summary>
    public int PlanDays { get; init; } = 30;

    /// <summary>Free days given the first time an approved driver opens the app. 0 turns the trial off.</summary>
    public int TrialDays { get; init; } = 30;

    /// <summary>Days a driver can keep working after their period ends, to give them time to renew.</summary>
    public int GraceDays { get; init; } = 3;

    /// <summary>The apps warn the driver this many days before the period ends.</summary>
    public int ExpiryWarningDays { get; init; } = 3;

    /// <summary>False until online payment (Razorpay) is wired up: the app then tells drivers to contact support.</summary>
    public bool PaymentsEnabled { get; init; }

    /// <summary>How often the background job takes lapsed drivers offline.</summary>
    public int ExpiryCheckMinutes { get; init; } = 10;
}

/// <param name="Enforced">False when subscriptions are switched off: everyone may take rides.</param>
/// <param name="ValidUntil">When the current paid-up period ends (null if there is none).</param>
/// <param name="GraceEndsAt">Last moment of the grace period, once the paid period has ended.</param>
public sealed record SubscriptionState(
    bool Enforced, SubscriptionPhase Phase, bool IsTrial, DateTimeOffset? ValidUntil, DateTimeOffset? GraceEndsAt, bool ExpiringSoon)
{
    public bool CanTakeRides => !Enforced || Phase is SubscriptionPhase.Active or SubscriptionPhase.Grace;

    /// <summary>Whole days left in the paid period (0 on the last day), or null when there is no paid period running.</summary>
    public int? DaysLeft(DateTimeOffset now) =>
        Phase == SubscriptionPhase.Active && ValidUntil is { } end ? Math.Max(0, (int)Math.Ceiling((end - now).TotalDays) - 1) : null;
}

public static class SubscriptionPolicy
{
    /// <summary>Works out what a driver's periods allow at <paramref name="now"/>.</summary>
    public static SubscriptionState Evaluate(IEnumerable<Subscription> periods, DateTimeOffset now, SubscriptionOptions options)
    {
        if (!options.Enabled)
            return new SubscriptionState(false, SubscriptionPhase.Active, false, null, null, false);

        // Cancelled or failed-payment periods give no access; they must not keep a driver "in grace" either.
        var counted = periods.Where(p => p.Status is SubscriptionStatus.Active or SubscriptionStatus.Expired).ToList();
        if (counted.Count == 0)
            return new SubscriptionState(true, SubscriptionPhase.None, false, null, null, false);

        // Periods can be queued back to back, so "valid until" is the end of the chain that covers now.
        var covering = counted.Where(p => p.IsActiveAt(now)).OrderByDescending(p => p.EndsAt).ToList();
        if (covering.Count > 0)
        {
            var end = counted.Max(p => p.EndsAt);
            var current = covering[0];
            var warn = end - now <= TimeSpan.FromDays(options.ExpiryWarningDays);
            return new SubscriptionState(true, SubscriptionPhase.Active, current.Kind == SubscriptionKind.Trial, end, null, warn);
        }

        var lastEnd = counted.Max(p => p.EndsAt);
        var graceEnd = lastEnd.AddDays(options.GraceDays);
        var lastWasTrial = counted.OrderByDescending(p => p.EndsAt).First().Kind == SubscriptionKind.Trial;
        return now < graceEnd
            ? new SubscriptionState(true, SubscriptionPhase.Grace, lastWasTrial, lastEnd, graceEnd, true)
            : new SubscriptionState(true, SubscriptionPhase.Expired, lastWasTrial, lastEnd, graceEnd, false);
    }
}
