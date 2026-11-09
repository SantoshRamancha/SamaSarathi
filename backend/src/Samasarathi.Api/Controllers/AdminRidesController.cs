using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Samasarathi.Application.Abstractions;
using Samasarathi.Application.Contracts;
using Samasarathi.Domain;
using Samasarathi.Domain.Entities;
using Samasarathi.Infrastructure.Persistence;

namespace Samasarathi.Api.Controllers;

/// <summary>Read-only view of rides for our team: what is happening now, what happened, and how riders and drivers rated each other.</summary>
[ApiController]
[Route("api/admin/rides")]
[Authorize(Roles = AppRoles.Admin)]
public class AdminRidesController(AppDbContext db, TimeProvider clock) : ControllerBase
{
    private static readonly RideStatus[] Unsuccessful = [RideStatus.Cancelled, RideStatus.Declined, RideStatus.Expired];

    [HttpGet("counts")]
    public async Task<AdminRideCounts> Counts(CancellationToken ct)
    {
        var since = clock.GetUtcNow().AddHours(-24);
        return new AdminRideCounts(
            Active: await db.Rides.CountAsync(r => Ride.ActiveStatuses.Contains(r.Status), ct),
            Completed24h: await db.Rides.CountAsync(r => r.Status == RideStatus.Completed && r.RequestedAt >= since, ct),
            Unsuccessful24h: await db.Rides.CountAsync(r => Unsuccessful.Contains(r.Status) && r.RequestedAt >= since, ct),
            Total: await db.Rides.CountAsync(ct));
    }

    /// <summary>Rides, newest first.</summary>
    /// <param name="search">Part of the rider's or driver's name or phone number, or the vehicle number.</param>
    [HttpGet]
    public async Task<IReadOnlyList<AdminRideSummary>> List(
        AdminRideFilter filter = AdminRideFilter.Active, string? search = null,
        [Range(0, 100_000)] int skip = 0, [Range(1, 100)] int take = 50, CancellationToken ct = default)
    {
        IQueryable<Ride> query = db.Rides;
        query = filter switch
        {
            AdminRideFilter.Active => query.Where(r => Ride.ActiveStatuses.Contains(r.Status)),
            AdminRideFilter.Completed => query.Where(r => r.Status == RideStatus.Completed),
            AdminRideFilter.Unsuccessful => query.Where(r => Unsuccessful.Contains(r.Status)),
            _ => query,
        };

        if (!string.IsNullOrWhiteSpace(search))
        {
            var like = $"%{search.Trim()}%";
            query = query.Where(r =>
                db.Riders.Any(x => x.Id == r.RiderId && (EF.Functions.ILike(x.FullName, like) || EF.Functions.ILike(x.Phone, like)))
                || db.Drivers.Any(x => x.Id == r.DriverId && (EF.Functions.ILike(x.FullName, like) || EF.Functions.ILike(x.Phone, like)))
                || db.Vehicles.Any(v => v.DriverId == r.DriverId && EF.Functions.ILike(v.RegistrationNumber, like)));
        }

        var rides = await query.OrderByDescending(r => r.RequestedAt).Skip(skip).Take(take).ToListAsync(ct);

        var riderIds = rides.Select(r => r.RiderId).Distinct().ToList();
        var driverIds = rides.Where(r => r.DriverId != null).Select(r => r.DriverId!.Value).Distinct().ToList();
        var riders = await db.Riders.Where(x => riderIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, ct);
        var drivers = await db.Drivers.Include(d => d.Vehicle).Where(x => driverIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, ct);

        return rides.Select(r =>
        {
            var rider = riders.GetValueOrDefault(r.RiderId);
            var driver = r.DriverId is { } id ? drivers.GetValueOrDefault(id) : null;
            return new AdminRideSummary(
                r.Id, r.Status, r.RequestedAt,
                rider?.FullName ?? "(deleted)", rider?.Phone ?? "", driver?.FullName, driver?.Phone, driver?.Vehicle?.RegistrationNumber,
                r.EstimatedDistanceKm, r.FinalFare ?? r.EstimatedFare, r.PricingMode, r.RatingForDriver, r.RatingForRider);
        }).ToList();
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<AdminRideDetail>> Get(Guid id, CancellationToken ct)
    {
        if (await db.Rides.FirstOrDefaultAsync(r => r.Id == id, ct) is not { } r) return NotFound();

        var rider = await db.Riders.FirstOrDefaultAsync(x => x.Id == r.RiderId, ct);
        var driver = r.DriverId is { } driverId
            ? await db.Drivers.Include(d => d.Vehicle).FirstOrDefaultAsync(d => d.Id == driverId, ct)
            : null;

        return new AdminRideDetail(
            r.Id, r.Status, r.PricingMode,
            rider?.FullName ?? "(deleted)", rider?.Phone ?? "",
            driver?.Id, driver?.FullName, driver?.Phone, driver?.Vehicle?.Type, driver?.Vehicle?.RegistrationNumber,
            r.Pickup.Y, r.Pickup.X, r.Drop.Y, r.Drop.X,
            r.RatePerKm, r.EstimatedDistanceKm, r.EstimatedMinutes, r.EstimatedFare,
            r.ActualMinutes, r.FinalFare,
            r.RequestedAt, r.AcceptedAt, r.StartedAt, r.CompletedAt, r.CancelledAt, r.CancelReason,
            r.RatingForDriver, r.CommentForDriver, r.RatingForRider, r.CommentForRider);
    }
}
