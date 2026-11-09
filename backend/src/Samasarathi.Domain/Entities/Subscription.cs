namespace Samasarathi.Domain.Entities;

/// <summary>
/// One paid-for (or free) period of a driver's fixed monthly fee. This replaces per-ride commission.
/// A driver's access is worked out from all their periods: see <see cref="SubscriptionState"/>.
/// </summary>
public class Subscription
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DriverId { get; set; }
    public required string PlanName { get; set; }
    public decimal MonthlyPrice { get; set; }
    public DateTimeOffset StartsAt { get; set; }
    public DateTimeOffset EndsAt { get; set; }
    public SubscriptionStatus Status { get; set; } = SubscriptionStatus.Active;

    /// <summary>How this period came about: the free trial, a payment, or our team granting it.</summary>
    public SubscriptionKind Kind { get; set; } = SubscriptionKind.Paid;

    /// <summary>Payment gateway's subscription/mandate id (e.g. Razorpay).</summary>
    public string? PaymentReference { get; set; }

    /// <summary>Why our team granted this period (support case, goodwill, ...).</summary>
    public string? Note { get; set; }

    /// <summary>Phone number of the admin who granted it, when <see cref="Kind"/> is Granted.</summary>
    public string? GrantedBy { get; set; }

    public bool IsActiveAt(DateTimeOffset now) =>
        Status == SubscriptionStatus.Active && StartsAt <= now && now < EndsAt;
}
