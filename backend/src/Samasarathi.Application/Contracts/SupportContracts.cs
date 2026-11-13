using Samasarathi.Domain;

namespace Samasarathi.Application.Contracts;

/// <summary>How to reach support. A value is null when it is not configured, so the apps show only what exists.</summary>
public sealed record SupportInfo(string? Phone, string? WhatsApp, string? Email, string? Hours)
{
    public bool Available => Phone is not null || WhatsApp is not null || Email is not null;

    /// <summary>Trims the configured values and turns blanks into null.</summary>
    public static SupportInfo From(SupportOptions options) =>
        new(Clean(options.Phone), Clean(options.WhatsApp), Clean(options.Email), Clean(options.Hours));

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
