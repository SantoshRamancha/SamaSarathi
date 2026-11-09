using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Samasarathi.Application.Abstractions;
using Samasarathi.Domain;
using Samasarathi.Infrastructure.Auth;
using Samasarathi.Infrastructure.Persistence;

namespace Samasarathi.Api.Controllers;

public sealed record OtpRequest([Required] string Phone);
public sealed record OtpVerifyRequest([Required] string Phone, [Required] string Code, [Required] string Role);
public sealed record AuthResponse(string Token, string Phone, string Role, bool IsRegistered);

/// <summary>Phone + OTP login. The same flow serves both apps; "role" says which one is logging in.</summary>
[ApiController]
[Route("api/auth")]
public class AuthController(IOtpService otp, ITokenService tokens, AppDbContext db, AdminOptions admins) : ControllerBase
{
    [HttpPost("otp/request")]
    public async Task<IActionResult> RequestOtp(OtpRequest req, CancellationToken ct)
    {
        if (!PhoneNumber.TryNormalize(req.Phone, out var phone))
            return Problem(detail: "Enter a valid 10-digit mobile number.", statusCode: 400);

        return await otp.SendAsync(phone, ct) == OtpSendResult.TooSoon
            ? Problem(detail: "Please wait 30 seconds before requesting another code.", statusCode: 429)
            : Accepted(new { phone });
    }

    [HttpPost("otp/verify")]
    public async Task<ActionResult<AuthResponse>> VerifyOtp(OtpVerifyRequest req, CancellationToken ct)
    {
        if (req.Role is not (AppRoles.Driver or AppRoles.Rider or AppRoles.Admin))
            return Problem(detail: "Unknown app role.", statusCode: 400);
        if (!PhoneNumber.TryNormalize(req.Phone, out var phone))
            return Problem(detail: "Enter a valid 10-digit mobile number.", statusCode: 400);
        if (!await otp.VerifyAsync(phone, req.Code.Trim(), ct))
            return Problem(detail: "Incorrect or expired code.", statusCode: 401);
        // Checked after the OTP, so only the owner of a number can find out whether it is an admin.
        if (req.Role == AppRoles.Admin && !admins.IsAdmin(phone))
            return Problem(detail: "This number does not have admin access.", statusCode: 403);

        var registered = req.Role switch
        {
            AppRoles.Driver => await db.Drivers.AnyAsync(d => d.Phone == phone, ct),
            AppRoles.Rider => await db.Riders.AnyAsync(r => r.Phone == phone, ct),
            _ => true,
        };

        return new AuthResponse(tokens.CreateToken(phone, req.Role), phone, req.Role, registered);
    }
}
