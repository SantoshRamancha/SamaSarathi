using System.Text;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Samasarathi.Application.Abstractions;

namespace Samasarathi.Infrastructure.Auth;

public sealed class JwtOptions
{
    /// <summary>Signing secret, at least 32 characters. Keep out of source control in production.</summary>
    public string Key { get; init; } = "";
    public string Issuer { get; init; } = "samasarathi";
    public string Audience { get; init; } = "samasarathi-apps";
    public int LifetimeDays { get; init; } = 30;

    /// <summary>Admin tokens can approve drivers, so they expire after one working day.</summary>
    public int AdminLifetimeHours { get; init; } = 12;

    public SymmetricSecurityKey SigningKey() => new(Encoding.UTF8.GetBytes(Key));
}

/// <summary>Issues the signed token the apps send with every request ("Authorization: Bearer ...").</summary>
public sealed class JwtTokenService(JwtOptions options) : ITokenService
{
    public string CreateToken(string phone, string role) =>
        new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = options.Issuer,
            Audience = options.Audience,
            Expires = role == AppRoles.Admin
                ? DateTime.UtcNow.AddHours(options.AdminLifetimeHours)
                : DateTime.UtcNow.AddDays(options.LifetimeDays),
            Claims = new Dictionary<string, object> { ["phone"] = phone, ["role"] = role },
            SigningCredentials = new SigningCredentials(options.SigningKey(), SecurityAlgorithms.HmacSha256),
        });
}
