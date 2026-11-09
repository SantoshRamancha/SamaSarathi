using Samasarathi.Domain;

namespace Samasarathi.Infrastructure.Auth;

/// <summary>Who may log in to the admin portal ("Admin" config section).</summary>
public sealed class AdminOptions
{
    /// <summary>Mobile numbers of the verification team, in any common format.</summary>
    public string[] Phones { get; init; } = [];

    public bool IsAdmin(string normalizedPhone) =>
        Phones.Any(p => PhoneNumber.TryNormalize(p, out var n) && n == normalizedPhone);
}
