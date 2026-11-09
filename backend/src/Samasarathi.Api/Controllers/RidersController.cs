using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Samasarathi.Application.Abstractions;
using Samasarathi.Domain;
using Samasarathi.Domain.Entities;
using Samasarathi.Infrastructure.Persistence;

namespace Samasarathi.Api.Controllers;

public sealed record RiderProfileRequest([Required, StringLength(120, MinimumLength = 2)] string FullName);
public sealed record RiderMeResponse(Guid Id, string FullName, string Phone);

/// <summary>A driver shown on the rider's map. No name or phone: those are shared only once a ride is booked.</summary>
public sealed record NearbyDriverDto(
    Guid Id, double Latitude, double Longitude, double DistanceKm, VehicleType VehicleType, decimal RatePerKm,
    double? RatingAverage, int RatingCount);

[ApiController]
[Route("api/riders/me")]
[Authorize(Roles = AppRoles.Rider)]
public class RidersController(
    AppDbContext db, IDriverLocationStore locations, LocationOptions locationOptions, RideOptions rideOptions,
    SubscriptionOptions subscriptionOptions, TimeProvider clock) : ControllerBase
{
    /// <summary>Online, verified drivers around the rider, nearest first.</summary>
    [HttpGet("nearby-drivers")]
    public async Task<IReadOnlyList<NearbyDriverDto>> NearbyDrivers(
        [Range(-90, 90)] double latitude, [Range(-180, 180)] double longitude, CancellationToken ct)
    {
        var nearby = await locations.FindNearbyAsync(latitude, longitude,
            locationOptions.NearbyRadiusKm, locationOptions.MaxNearbyDrivers, clock.GetUtcNow(), ct);
        if (nearby.Count == 0) return [];

        // Redis knows where drivers are; the database confirms they are still verified and online.
        var ids = nearby.Select(n => n.DriverId).ToList();
        var now = clock.GetUtcNow();
        var drivers = (await db.Drivers.Include(d => d.Vehicle).Include(d => d.Subscriptions)
                .Where(d => ids.Contains(d.Id) && d.IsOnline && d.VerificationStatus == VerificationStatus.Approved)
                .ToListAsync(ct))
            .Where(d => d.CanAcceptRides(now, subscriptionOptions)) // a lapsed subscription hides the driver at once
            .ToDictionary(d => d.Id);

        // A driver who already has a ride (waiting for their answer, or accepted) can't take another.
        // A request nobody answered in time no longer counts, even if nothing has marked it Expired yet.
        var offerCutoff = now - rideOptions.OfferTimeout;
        var busy = await db.Rides
            .Where(r => r.DriverId != null && ids.Contains(r.DriverId.Value) && Ride.ActiveStatuses.Contains(r.Status)
                        && (r.Status != RideStatus.Requested || r.RequestedAt > offerCutoff))
            .Select(r => r.DriverId!.Value).ToListAsync(ct);

        var ratings = await RatingQueries.ForDriversAsync(db, ids, ct);

        return nearby
            .Where(n => drivers.ContainsKey(n.DriverId) && drivers[n.DriverId].Vehicle is not null && !busy.Contains(n.DriverId))
            .Select(n =>
            {
                var d = drivers[n.DriverId];
                var rating = ratings.GetValueOrDefault(d.Id);
                return new NearbyDriverDto(d.Id, n.Latitude, n.Longitude, Math.Round(n.DistanceKm, 2), d.Vehicle!.Type, d.RatePerKm,
                    rating?.Average, rating?.Count ?? 0);
            })
            .ToList();
    }

    private string Phone => User.FindFirst("phone")!.Value;

    [HttpGet]
    public async Task<ActionResult<RiderMeResponse>> Get(CancellationToken ct) =>
        await db.Riders.FirstOrDefaultAsync(r => r.Phone == Phone, ct) is { } r
            ? new RiderMeResponse(r.Id, r.FullName, r.Phone)
            : NotFound();

    [HttpPut]
    public async Task<ActionResult<RiderMeResponse>> Save(RiderProfileRequest req, CancellationToken ct)
    {
        var rider = await db.Riders.FirstOrDefaultAsync(r => r.Phone == Phone, ct);
        if (rider is null)
        {
            rider = new Rider { FullName = req.FullName.Trim(), Phone = Phone };
            db.Riders.Add(rider);
        }
        else
        {
            rider.FullName = req.FullName.Trim();
        }

        await db.SaveChangesAsync(ct);
        return new RiderMeResponse(rider.Id, rider.FullName, rider.Phone);
    }
}
