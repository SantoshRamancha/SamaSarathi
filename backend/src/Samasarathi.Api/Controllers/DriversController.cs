using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Samasarathi.Application.Abstractions;
using Samasarathi.Domain;
using Samasarathi.Domain.Entities;
using Samasarathi.Infrastructure.Persistence;

namespace Samasarathi.Api.Controllers;

public sealed record DriverProfileRequest([Required, StringLength(120, MinimumLength = 2)] string FullName);
public sealed record DriverVehicleRequest(VehicleType Type, [Required] string RegistrationNumber, [StringLength(60)] string? Model);
public sealed record DriverRateRequest(decimal RatePerKm);
public sealed record LocationRequest([Range(-90, 90)] double Latitude, [Range(-180, 180)] double Longitude);

/// <summary>One period in the driver's subscription history.</summary>
public sealed record SubscriptionPeriodDto(
    SubscriptionKind Kind, string PlanName, decimal Price, DateTimeOffset StartsAt, DateTimeOffset EndsAt, string? Note);

/// <summary>The driver's subscription as the app shows it.</summary>
/// <param name="Enforced">False when subscriptions are switched off: everyone may take rides.</param>
/// <param name="DaysLeft">Days left in the paid period; null when there isn't one running.</param>
/// <param name="GraceEndsAt">While in grace: the last moment the driver can still go online.</param>
/// <param name="PaymentsEnabled">False until online payment exists: the app then asks drivers to contact support.</param>
public sealed record SubscriptionDto(
    bool Enforced, SubscriptionPhase Phase, bool IsTrial, bool CanTakeRides, DateTimeOffset? ValidUntil,
    DateTimeOffset? GraceEndsAt, int? DaysLeft, bool ExpiringSoon, string PlanName, decimal MonthlyPrice, bool PaymentsEnabled,
    IReadOnlyList<SubscriptionPeriodDto> History)
{
    public static SubscriptionDto From(Driver d, DateTimeOffset now, SubscriptionOptions options)
    {
        var state = d.SubscriptionAt(now, options);
        return new SubscriptionDto(
            state.Enforced, state.Phase, state.IsTrial, state.CanTakeRides, state.ValidUntil, state.GraceEndsAt,
            state.DaysLeft(now), state.ExpiringSoon, options.PlanName, options.MonthlyPrice, options.PaymentsEnabled,
            d.Subscriptions
                .Where(s => s.Status is SubscriptionStatus.Active or SubscriptionStatus.Expired)
                .OrderByDescending(s => s.StartsAt)
                .Select(s => new SubscriptionPeriodDto(s.Kind, s.PlanName, s.MonthlyPrice, s.StartsAt, s.EndsAt, s.Note))
                .ToList());
    }
}

public sealed record VehicleDto(VehicleType Type, string RegistrationNumber, string? Model);
public sealed record DocumentDto(DocumentType Type, string FileName, long SizeBytes, DateTimeOffset UploadedAt);
public sealed record CheckDto(VerificationCheckType Type, CheckResult Result, string? Notes);

public sealed record DriverMeResponse(
    Guid Id, string FullName, string Phone, decimal RatePerKm,
    VerificationStatus VerificationStatus, bool CanEdit, DateTimeOffset? SubmittedAt, string? RejectionReason,
    bool IsOnline, VehicleDto? Vehicle, IReadOnlyList<DocumentDto> Documents,
    IReadOnlyList<OnboardingStep> MissingSteps, IReadOnlyList<CheckDto> Checks)
{
    public static DriverMeResponse From(Driver d) => new(
        d.Id, d.FullName, d.Phone, d.RatePerKm, d.VerificationStatus, d.CanEditOnboarding, d.SubmittedAt, d.RejectionReason,
        d.IsOnline,
        d.Vehicle is { } v ? new VehicleDto(v.Type, v.RegistrationNumber, v.Model) : null,
        d.Documents.OrderBy(x => x.Type).Select(x => new DocumentDto(x.Type, x.OriginalFileName, x.SizeBytes, x.UploadedAt)).ToList(),
        d.MissingSteps(),
        d.CurrentChecks().Select(c => new CheckDto(c.Type, c.Result, c.Notes)).ToList());
}

/// <summary>Driver registration, step by step. Everything acts on the logged-in driver ("me").</summary>
[ApiController]
[Route("api/drivers/me")]
[Authorize(Roles = AppRoles.Driver)]
public class DriversController(
    AppDbContext db, IFileStorage files, IDriverLocationStore locations, RideOptions rideOptions,
    SubscriptionOptions subscriptionOptions, TimeProvider clock) : ControllerBase
{
    private const long MaxDocumentBytes = 5 * 1024 * 1024;
    private static readonly Dictionary<string, string> AllowedTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".jpg"] = "image/jpeg", [".jpeg"] = "image/jpeg", [".png"] = "image/png", [".pdf"] = "application/pdf",
    };

    private string Phone => User.FindFirst("phone")!.Value;

    [HttpGet]
    public async Task<ActionResult<DriverMeResponse>> Get(CancellationToken ct) =>
        await Load(ct) is { } driver ? DriverMeResponse.From(driver) : NotFound();

    /// <summary>Step 1. Creates the driver record on first save.</summary>
    [HttpPut("profile")]
    public async Task<ActionResult<DriverMeResponse>> SaveProfile(DriverProfileRequest req, CancellationToken ct)
    {
        var driver = await Load(ct);
        if (driver is null)
        {
            driver = new Driver { FullName = req.FullName.Trim(), Phone = Phone };
            db.Drivers.Add(driver);
        }
        else
        {
            if (Locked(driver) is { } locked) return locked;
            driver.FullName = req.FullName.Trim();
        }

        await db.SaveChangesAsync(ct);
        return DriverMeResponse.From(driver);
    }

    [HttpPut("vehicle")]
    public async Task<ActionResult<DriverMeResponse>> SaveVehicle(DriverVehicleRequest req, CancellationToken ct)
    {
        if (await Load(ct) is not { } driver) return ProfileFirst();
        if (Locked(driver) is { } locked) return locked;
        if (!VehicleNumber.TryNormalize(req.RegistrationNumber, out var number))
            return Problem(detail: "Enter a valid vehicle number, e.g. KA01AB1234.", statusCode: 400);
        if (await db.Vehicles.AnyAsync(v => v.RegistrationNumber == number && v.DriverId != driver.Id, ct))
            return Problem(detail: "This vehicle is already registered with another driver.", statusCode: 409);

        driver.Vehicle ??= new Vehicle { DriverId = driver.Id, RegistrationNumber = number };
        driver.Vehicle.Type = req.Type;
        driver.Vehicle.RegistrationNumber = number;
        driver.Vehicle.Model = string.IsNullOrWhiteSpace(req.Model) ? null : req.Model.Trim();

        await db.SaveChangesAsync(ct);
        return DriverMeResponse.From(driver);
    }

    /// <summary>Uploads or replaces one document. Sent as multipart form: "type" + "file".</summary>
    [HttpPost("documents")]
    [RequestSizeLimit(MaxDocumentBytes + 64 * 1024)]
    public async Task<ActionResult<DriverMeResponse>> UploadDocument(
        [FromForm] DocumentType type, IFormFile file, CancellationToken ct)
    {
        if (await Load(ct) is not { } driver) return ProfileFirst();
        if (Locked(driver) is { } locked) return locked;

        var extension = Path.GetExtension(file.FileName);
        if (!AllowedTypes.TryGetValue(extension, out var contentType))
            return Problem(detail: "Upload a JPG, PNG or PDF file.", statusCode: 400);
        if (file.Length is 0 or > MaxDocumentBytes)
            return Problem(detail: "File must be between 1 byte and 5 MB.", statusCode: 400);

        await using var stream = file.OpenReadStream();
        var key = await files.SaveAsync(stream, extension, ct);

        var old = driver.Documents.FirstOrDefault(d => d.Type == type);
        if (old is not null)
        {
            driver.Documents.Remove(old);
            db.DriverDocuments.Remove(old);
        }
        driver.Documents.Add(new DriverDocument
        {
            DriverId = driver.Id, Type = type, StorageKey = key,
            OriginalFileName = Path.GetFileName(file.FileName), ContentType = contentType, SizeBytes = file.Length,
        });

        await db.SaveChangesAsync(ct);
        if (old is not null) await files.DeleteAsync(old.StorageKey, ct);
        return DriverMeResponse.From(driver);
    }

    [HttpPut("rate")]
    public async Task<ActionResult<DriverMeResponse>> SaveRate(DriverRateRequest req, CancellationToken ct)
    {
        if (await Load(ct) is not { } driver) return ProfileFirst();
        if (Locked(driver) is { } locked) return locked;
        if (req.RatePerKm is < Driver.MinRatePerKm or > Driver.MaxRatePerKm)
            return Problem(detail: $"Rate must be between ₹{Driver.MinRatePerKm} and ₹{Driver.MaxRatePerKm} per km.", statusCode: 400);

        driver.RatePerKm = Math.Round(req.RatePerKm, 2);
        await db.SaveChangesAsync(ct);
        return DriverMeResponse.From(driver);
    }

    [HttpPost("submit")]
    public async Task<ActionResult<DriverMeResponse>> Submit(CancellationToken ct)
    {
        if (await Load(ct) is not { } driver) return ProfileFirst();
        try
        {
            driver.SubmitForVerification(clock.GetUtcNow());
        }
        catch (InvalidOperationException ex)
        {
            return Problem(detail: ex.Message, statusCode: 400);
        }

        await db.SaveChangesAsync(ct);
        return DriverMeResponse.From(driver);
    }

    /// <summary>Starts taking rides. Riders nearby see this driver on their map.</summary>
    [HttpPost("online")]
    public async Task<ActionResult<DriverMeResponse>> GoOnline(LocationRequest req, CancellationToken ct)
    {
        await StartTrialAsync(ct);
        return await Locate(d =>
        {
            var now = clock.GetUtcNow();
            d.EnsureNotSuspended(); // say "suspended" before talking about the subscription
            d.EnsureSubscribed(now, subscriptionOptions);
            d.GoOnline(req.Latitude, req.Longitude, now);
        }, ct);
    }

    /// <summary>
    /// Called every few seconds while online. If it stops, the driver drops off riders' maps after a minute.
    /// A driver whose subscription has run out, or who has been suspended, is taken offline here too
    /// (unless a ride is under way: they finish it first).
    /// </summary>
    [HttpPut("location")]
    public async Task<ActionResult<DriverMeResponse>> UpdateLocation(LocationRequest req, CancellationToken ct)
    {
        if (await Load(ct) is { IsOnline: true } driver
            && !driver.CanAcceptRides(clock.GetUtcNow(), subscriptionOptions)
            && !await db.Rides.AnyAsync(r => r.DriverId == driver.Id && Ride.ActiveStatuses.Contains(r.Status), ct))
        {
            driver.GoOffline();
            await db.SaveChangesAsync(ct);
            await locations.RemoveAsync(driver.Id, ct);
            return Problem(
                detail: driver.VerificationStatus == VerificationStatus.Suspended
                    ? $"Your account was suspended, so you were taken offline.{(string.IsNullOrWhiteSpace(driver.RejectionReason) ? "" : $" {driver.RejectionReason}")}"
                    : "Your subscription has ended, so you were taken offline. Renew it to go online.",
                statusCode: 409);
        }

        return await Locate(d => d.UpdateLocation(req.Latitude, req.Longitude, clock.GetUtcNow()), ct);
    }

    /// <summary>
    /// The driver's plan, how long it is valid and the history of periods. The first time an approved driver asks,
    /// the free trial (if the plan has one) starts.
    /// </summary>
    [HttpGet("subscription")]
    public async Task<ActionResult<SubscriptionDto>> GetSubscription(CancellationToken ct)
    {
        if (await Load(ct) is not { } driver) return NotFound();
        await StartTrialAsync(ct, driver);
        return SubscriptionDto.From(driver, clock.GetUtcNow(), subscriptionOptions);
    }

    /// <summary>Starts the free trial for an approved driver who has never had a subscription. Safe to call every time.</summary>
    private async Task StartTrialAsync(CancellationToken ct, Driver? driver = null)
    {
        driver ??= await Load(ct);
        if (driver is not null && driver.StartTrialIfNew(clock.GetUtcNow(), subscriptionOptions))
            await db.SaveChangesAsync(ct);
    }

    /// <summary>The stars riders have given this driver.</summary>
    [HttpGet("rating")]
    public async Task<ActionResult<RatingSummaryDto>> Rating(CancellationToken ct) =>
        await db.Drivers.Where(d => d.Phone == Phone).Select(d => (Guid?)d.Id).FirstOrDefaultAsync(ct) is { } id
            ? await RatingQueries.ForDriverAsync(db, id, ct)
            : RatingSummaryDto.None;

    [HttpPost("offline")]
    public async Task<ActionResult<DriverMeResponse>> GoOffline(CancellationToken ct)
    {
        if (await Load(ct) is not { } driver) return ProfileFirst();

        // Going offline with a rider waiting (or on board) would strand them. A request nobody answered in time doesn't count.
        var now = clock.GetUtcNow();
        var offerCutoff = now - rideOptions.OfferTimeout;
        if (await db.Rides.AnyAsync(r => r.DriverId == driver.Id && Ride.ActiveStatuses.Contains(r.Status)
                                         && (r.Status != RideStatus.Requested || r.RequestedAt > offerCutoff), ct))
            return Problem(detail: "Finish or cancel your current ride before going offline.", statusCode: 409);

        driver.GoOffline();
        await db.SaveChangesAsync(ct);
        await locations.RemoveAsync(driver.Id, ct);
        return DriverMeResponse.From(driver);
    }

    /// <summary>Applies an online/location rule, saves it, and mirrors the position into the live map (Redis).</summary>
    private async Task<ActionResult<DriverMeResponse>> Locate(Action<Driver> action, CancellationToken ct)
    {
        if (await Load(ct) is not { } driver) return ProfileFirst();
        try
        {
            action(driver);
        }
        catch (InvalidOperationException ex)
        {
            return Problem(detail: ex.Message, statusCode: 409);
        }
        catch (ArgumentOutOfRangeException)
        {
            return Problem(detail: "Location is not a valid latitude/longitude.", statusCode: 400);
        }

        await db.SaveChangesAsync(ct);
        var at = driver.LastLocationAt!.Value;
        await locations.UpdateAsync(driver.Id, driver.LastKnownLocation!.Y, driver.LastKnownLocation.X, at, ct);
        return DriverMeResponse.From(driver);
    }

    private Task<Driver?> Load(CancellationToken ct) =>
        db.Drivers
            .Include(d => d.Vehicle)
            .Include(d => d.Documents)
            .Include(d => d.VerificationChecks)
            .Include(d => d.Subscriptions)
            .AsSplitQuery()
            .FirstOrDefaultAsync(d => d.Phone == Phone, ct);

    private ObjectResult? Locked(Driver driver) => driver.CanEditOnboarding
        ? null
        : Problem(detail: "Your details are with our verification team and can't be changed now.", statusCode: 409);

    private ObjectResult ProfileFirst() =>
        Problem(detail: "Complete your profile first.", statusCode: 400);
}
