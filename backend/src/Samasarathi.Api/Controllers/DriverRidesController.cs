using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Samasarathi.Application.Abstractions;
using Samasarathi.Domain;
using Samasarathi.Domain.Entities;
using Samasarathi.Domain.Pricing;
using Samasarathi.Infrastructure.Persistence;

namespace Samasarathi.Api.Controllers;

/// <summary>Driver side of a ride: see the request a rider sent, accept or decline it, cancel after accepting.</summary>
[ApiController]
[Route("api/drivers/me/ride")]
[Authorize(Roles = AppRoles.Driver)]
public class DriverRidesController(
    AppDbContext db, RideOptions rideOptions, FareCalculator calculator, IRouteService routes,
    SubscriptionOptions subscriptionOptions, TimeProvider clock) : ControllerBase
{
    private string Phone => User.FindFirst("phone")!.Value;

    /// <summary>The request waiting for an answer, or the accepted ride. 204 when there is none.</summary>
    [HttpGet]
    public async Task<ActionResult<DriverRideDto>> Current(CancellationToken ct)
    {
        if (await db.Drivers.FirstOrDefaultAsync(d => d.Phone == Phone, ct) is not { } driver) return NoContent();

        var ride = await db.Rides.Where(r => r.DriverId == driver.Id && Ride.ActiveStatuses.Contains(r.Status))
            .OrderByDescending(r => r.RequestedAt).FirstOrDefaultAsync(ct);
        if (ride is null) return NoContent();

        if (ride.ExpireIfStale(clock.GetUtcNow(), rideOptions.OfferTimeout))
        {
            await db.SaveChangesAsync(ct);
            return NoContent();
        }

        return await Dto(ride, driver, ct);
    }

    /// <summary>
    /// Turn-by-turn style guidance for the map: the road from the driver (at lat/lng) to where they need to go next.
    /// Accepted: to the pickup. Arrived or in progress: to the drop-off (from the pickup while still waiting there).
    /// Returns an empty route when there is nothing to navigate or routing is unavailable; the map then just shows pins.
    /// </summary>
    [HttpGet("route")]
    public async Task<ActionResult<DriverRouteDto>> NavigationRoute(
        [FromQuery, Range(-90, 90)] double lat, [FromQuery, Range(-180, 180)] double lng, CancellationToken ct)
    {
        if (await db.Drivers.FirstOrDefaultAsync(d => d.Phone == Phone, ct) is not { } driver) return NoContent();
        var ride = await db.Rides.Where(r => r.DriverId == driver.Id && Ride.ActiveStatuses.Contains(r.Status))
            .OrderByDescending(r => r.RequestedAt).FirstOrDefaultAsync(ct);
        if (ride is null || ride.Status == RideStatus.Requested) return new DriverRouteDto([]);

        var (fromLat, fromLng, toLat, toLng) = ride.Status switch
        {
            RideStatus.Accepted => (lat, lng, ride.Pickup.Y, ride.Pickup.X),
            RideStatus.DriverArrived => (ride.Pickup.Y, ride.Pickup.X, ride.Drop.Y, ride.Drop.X),
            _ => (lat, lng, ride.Drop.Y, ride.Drop.X),
        };

        var route = await routes.TryGetRouteAsync(fromLat, fromLng, toLat, toLng, ct);
        return new DriverRouteDto(route?.Geometry.Select(p => new[] { p.Latitude, p.Longitude }).ToList() ?? []);
    }

    /// <summary>
    /// Takes a new request. A driver whose subscription ended while the request was waiting can't accept it.
    /// Every later step (arrived, start, complete, cancel) stays allowed so a trip under way is never interrupted.
    /// </summary>
    [HttpPost("{id:guid}/accept")]
    public Task<ActionResult<DriverRideDto>> Accept(Guid id, CancellationToken ct) =>
        Respond(id, (r, d) =>
        {
            var now = clock.GetUtcNow();
            d.EnsureNotSuspended();
            d.EnsureSubscribed(now, subscriptionOptions, action: "accept rides");
            r.Accept(d.Id, now);
        }, ct);

    [HttpPost("{id:guid}/decline")]
    public Task<ActionResult<DriverRideDto>> Decline(Guid id, CancellationToken ct) =>
        Respond(id, (r, d) => r.Decline(d.Id), ct);

    /// <summary>The driver is at the pickup point.</summary>
    [HttpPost("{id:guid}/arrived")]
    public Task<ActionResult<DriverRideDto>> Arrived(Guid id, CancellationToken ct) =>
        Respond(id, (r, d) => r.MarkArrived(d.Id), ct);

    /// <summary>The rider got in: the trip (and its clock) starts.</summary>
    [HttpPost("{id:guid}/start")]
    public Task<ActionResult<DriverRideDto>> Start(Guid id, CancellationToken ct) =>
        Respond(id, (r, d) => r.Start(d.Id, clock.GetUtcNow()), ct);

    /// <summary>Drop-off: works out the final fare. The response carries it so the driver can see the summary.</summary>
    [HttpPost("{id:guid}/complete")]
    public Task<ActionResult<DriverRideDto>> Complete(Guid id, CancellationToken ct) =>
        Respond(id, (r, d) => r.Complete(d.Id, clock.GetUtcNow(), calculator), ct);

    /// <summary>After the trip: 1 to 5 stars for the rider and an optional comment. Once only.</summary>
    [HttpPost("{id:guid}/rating")]
    public Task<ActionResult<DriverRideDto>> Rate(Guid id, RatingRequest req, CancellationToken ct) =>
        Respond(id, (r, d) => r.RateRider(d.Id, req.Stars, req.Comment, clock.GetUtcNow()), ct);

    /// <summary>Past rides, newest first: completed, declined, expired and cancelled ones (not the one in progress).</summary>
    [HttpGet("~/api/drivers/me/rides/history")]
    public async Task<IReadOnlyList<RideHistoryItem>> History(
        [Range(0, 10_000)] int skip = 0, [Range(1, 50)] int take = 20, CancellationToken ct = default)
    {
        if (await db.Drivers.FirstOrDefaultAsync(d => d.Phone == Phone, ct) is not { } driver) return [];

        var rides = await db.Rides
            .Where(r => r.DriverId == driver.Id && !Ride.ActiveStatuses.Contains(r.Status))
            .OrderByDescending(r => r.RequestedAt)
            .Skip(skip).Take(take)
            .ToListAsync(ct);

        var riderIds = rides.Select(r => r.RiderId).Distinct().ToList();
        var riders = await db.Riders.Where(r => riderIds.Contains(r.Id)).ToDictionaryAsync(r => r.Id, ct);

        return rides.Select(r => new RideHistoryItem(
            r.Id, r.Status, r.RequestedAt, r.CompletedAt ?? r.CancelledAt,
            r.EstimatedDistanceKm, r.FinalFare ?? r.EstimatedFare, r.PricingMode,
            riders.GetValueOrDefault(r.RiderId)?.FullName ?? "Rider", null,
            r.RatingForRider, r.RatingForDriver, r.Status == RideStatus.Completed && r.RatingForRider is null, r.CancelReason)).ToList();
    }

    /// <summary>For when the driver accepted but can't make it.</summary>
    [HttpPost("{id:guid}/cancel")]
    public Task<ActionResult<DriverRideDto>> Cancel(Guid id, CancellationToken ct) =>
        Respond(id, (r, _) => r.Cancel(byDriver: true, clock.GetUtcNow()), ct);

    private async Task<ActionResult<DriverRideDto>> Respond(Guid id, Action<Ride, Driver> action, CancellationToken ct)
    {
        if (await db.Drivers.Include(d => d.Subscriptions).FirstOrDefaultAsync(d => d.Phone == Phone, ct) is not { } driver) return NotFound();
        var ride = await db.Rides.FirstOrDefaultAsync(r => r.Id == id && r.DriverId == driver.Id, ct);
        if (ride is null) return NotFound();

        // A request the driver sat on too long has already lapsed: say so rather than accepting a dead request.
        if (ride.ExpireIfStale(clock.GetUtcNow(), rideOptions.OfferTimeout)) await db.SaveChangesAsync(ct);
        try
        {
            action(ride, driver);
        }
        catch (InvalidOperationException ex)
        {
            return Problem(detail: ex.Message, statusCode: 409);
        }

        await db.SaveChangesAsync(ct);
        return await Dto(ride, driver, ct);
    }

    private async Task<DriverRideDto> Dto(Ride ride, Driver driver, CancellationToken ct)
    {
        var rider = await db.Riders.FirstAsync(r => r.Id == ride.RiderId, ct);
        return DriverRideDto.From(ride, driver, rider, rideOptions.OfferTimeout);
    }
}
