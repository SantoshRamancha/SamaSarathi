namespace Samasarathi.Domain;

/// <summary>
/// "Support" config section: how riders and drivers reach the Samasarathi team. Edit in appsettings (or environment
/// variables such as <c>Support__WhatsApp</c>) and restart the API; the apps pick the new details up without a release.
/// Leave a value empty to hide that way of contacting us. These are public contact details, never secrets.
/// </summary>
public sealed class SupportOptions
{
    /// <summary>Number shown for calls, with country code, e.g. "+919876543210".</summary>
    public string? Phone { get; init; }

    /// <summary>WhatsApp number with country code, e.g. "+919876543210". Often the same as <see cref="Phone"/>.</summary>
    public string? WhatsApp { get; init; }

    public string? Email { get; init; }

    /// <summary>Free text shown under the buttons, e.g. "Mon–Sat, 9 am to 7 pm".</summary>
    public string? Hours { get; init; }
}
