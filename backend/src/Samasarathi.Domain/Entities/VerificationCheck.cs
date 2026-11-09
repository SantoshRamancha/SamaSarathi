namespace Samasarathi.Domain.Entities;

/// <summary>One background-verification step for a driver (licence, police record, skill test...).</summary>
public class VerificationCheck
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DriverId { get; set; }
    public VerificationCheckType Type { get; set; }
    public CheckResult Result { get; set; } = CheckResult.Pending;

    /// <summary>When this check was opened. Each submission opens a fresh set; older ones are kept as history.</summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Who performed it: an external KYC provider or our own reviewer.</summary>
    public string? Provider { get; set; }
    public string? ProviderReference { get; set; }
    public string? Notes { get; set; }
    public DateTimeOffset? CheckedAt { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
}
