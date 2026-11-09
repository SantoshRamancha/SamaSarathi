using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Samasarathi.Application;
using Samasarathi.Application.Abstractions;
using Samasarathi.Domain;
using Samasarathi.Domain.Entities;
using Samasarathi.Domain.Pricing;
using Samasarathi.Infrastructure.Persistence;

namespace Samasarathi.Api.Controllers;

public sealed record TripQuery(
    Guid DriverId,
    [Range(-90, 90)] double PickupLatitude, [Range(-180, 180)] double PickupLongitude,
    [Range(-90, 90)] double DropLatitude, [Range(-180, 180)] double DropLongitude);

/// <param name="IsRoadRoute">False when routing was unavailable and the distance is a straight-line estimate.</param>
/// <param name="IsWeatherKnown">False when the weather service was unreachable; the trip is then priced as dry.</param>
/// <param name="Route">The road route as [latitude, longitude] points, for drawing on the map (empty if there is none).</param>
public sealed record RideQuoteDto(
    decimal DistanceKm, decimal Minutes, decimal Fare, decimal RatePerKm, PricingMode Mode,
    bool IsPeakHour, bool IsRaining, bool IsRoadRoute, bool IsWeatherKnown, IReadOnlyList<double[]> Route)
{
    public static RideQuoteDto From(TripPlan plan, decimal ratePerKm) => new(
        plan.Estimate.DistanceKm, plan.Estimate.Minutes, plan.Estimate.Quote.Fare, ratePerKm, plan.Estimate.Quote.Mode,
        plan.Conditions.IsPeakHour, plan.Conditions.IsRaining, plan.Source == RouteSource.Road, plan.WeatherKnown,
        plan.Route.Select(p => new[] { p.Latitude, p.Longitude }).ToList());
}

/// <summary>Rider side of a ride: see the price, request a driver from the map, follow the request, cancel.</summary>
[ApiController]
[Route("api/rides")]
[Authorize(Roles = AppRoles.Rider)]
public class RidesController(
    AppDbContext db, TripPlanner planner, IRouteService routes, RideOptions rideOptions, LocationOptions locationOptions,
    SubscriptionOptions subscriptionOptions, TimeProvider clock) : ControllerBase
{
    private string Phone => User.FindFirst("phone")!.Value;

    /// <summary>The price for this trip with this driver, before the rider commits.</summary>
    [HttpGet("quote")]
    public async Task<ActionResult<RideQuoteDto>> Quote([FromQuery] TripQuery q, CancellationToken ct)
    {
        var driver = await db.Drivers.FirstOrDefaultAsync(d => d.Id == q.DriverId, ct);
        if (driver is null) return Problem(detail: "Driver not found.", statusCode: 404);
        try
        {
            var plan = await planner.PlanAsync(driver.RatePerKm, q.PickupLatitude, q.PickupLongitude, q.DropLatitude, q.DropLongitude, ct);
            return RideQuoteDto.From(plan, driver.RatePerKm);
        }
        catch (ArgumentException ex)
        {
            return Problem(detail: ex.Message, statusCode: 400);
        }
    }

    /// <summary>Asks one driver for a ride. They have <see cref="RideOptions.OfferTimeoutSeconds"/> seconds to accept.</summary>
    [HttpPost]
    public async Task<ActionResult<RideDto>> RequestRide(TripQuery req, CancellationToken ct)
    {
        var rider = await db.Riders.FirstOrDefaultAsync(r => r.Phone == Phone, ct);
        if (rider is null) return Problem(detail: "Complete your profile first.", statusCode: 400);

        var now = clock.GetUtcNow();
        var driver = await db.Drivers.Include(d => d.Vehicle).Include(d => d.Subscriptions)
            .FirstOrDefaultAsync(d => d.Id == req.DriverId, ct);
        var reporting = driver?.LastLocationAt is { } at && now - at <= TimeSpan.FromSeconds(locationOptions.StaleAfterSeconds);
        if (driver is null || driver.Vehicle is null || !driver.IsOnline || !reporting
            || !driver.CanAcceptRides(now, subscriptionOptions))
            return Problem(detail: "This driver isn't available any more. Pick another.", statusCode: 409);

        RideEstimate estimate;
        try
        {
            estimate = (await planner.PlanAsync(
                driver.RatePerKm, req.PickupLatitude, req.PickupLongitude, req.DropLatitude, req.DropLongitude, ct)).Estimate;
        }
        catch (ArgumentException ex)
        {
            return Problem(detail: ex.Message, statusCode: 400);
        }

        // Expire anything stale first so an old unanswered request doesn't block a new one.
        await ExpireStale(db.Rides.Where(r => (r.RiderId == rider.Id || r.DriverId == driver.Id) && r.Status == RideStatus.Requested), ct);

        if (await db.Rides.AnyAsync(r => r.RiderId == rider.Id && Ride.ActiveStatuses.Contains(r.Status), ct))
            return Problem(detail: "You already have a ride in progress. Cancel it first.", statusCode: 409);
        if (await db.Rides.AnyAsync(r => r.DriverId == driver.Id && Ride.ActiveStatuses.Contains(r.Status), ct))
            return Problem(detail: "This driver was just booked by someone else. Pick another.", statusCode: 409);

        var ride = Ride.Request(rider.Id, driver, req.PickupLatitude, req.PickupLongitude, req.DropLatitude, req.DropLongitude, estimate, now);
        db.Rides.Add(ride);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // The unique indexes caught a request that raced past the checks above.
            return Problem(detail: "This driver was just booked by someone else. Pick another.", statusCode: 409);
        }

        return RideDto.From(ride, driver, rideOptions.OfferTimeout);
    }

    /// <summary>Past rides, newest first: completed, declined, expired and cancelled ones (not the one in progress).</summary>
    [HttpGet("history")]
    public async Task<IReadOnlyList<RideHistoryItem>> History(
        [Range(0, 10_000)] int skip = 0, [Range(1, 50)] int take = 20, CancellationToken ct = default)
    {
        if (await CurrentRiderId(ct) is not { } riderId) return [];

        var rides = await db.Rides
            .Where(r => r.RiderId == riderId && !Ride.ActiveStatuses.Contains(r.Status))
            .OrderByDescending(r => r.RequestedAt)
            .Skip(skip).Take(take)
            .ToListAsync(ct);

        var driverIds = rides.Where(r => r.DriverId != null).Select(r => r.DriverId!.Value).Distinct().ToList();
        var drivers = await db.Drivers.Include(d => d.Vehicle).Where(d => driverIds.Contains(d.Id)).ToDictionaryAsync(d => d.Id, ct);

        return rides.Select(r =>
        {
            var d = r.DriverId is { } id ? drivers.GetValueOrDefault(id) : null;
            return new RideHistoryItem(
                r.Id, r.Status, r.RequestedAt, r.CompletedAt ?? r.CancelledAt,
                r.EstimatedDistanceKm, r.FinalFare ?? r.EstimatedFare, r.PricingMode,
                d?.FullName ?? "Driver", d?.Vehicle is { } v ? $"{v.Type} · {v.RegistrationNumber}" : null,
                r.RatingForDriver, r.RatingForRider, r.Status == RideStatus.Completed && r.RatingForDriver is null, r.CancelReason);
        }).ToList();
    }

    /// <summary>The rider's ride in progress, if any (used to pick up where they left off after reopening the app).</summary>
    [HttpGet("active")]
    public async Task<ActionResult<RideDto>> Active(CancellationToken ct)
    {
        if (await CurrentRiderId(ct) is not { } riderId) return NoContent();
        await ExpireStale(db.Rides.Where(r => r.RiderId == riderId && r.Status == RideStatus.Requested), ct);
        var ride = await db.Rides.Where(r => r.RiderId == riderId && Ride.ActiveStatuses.Contains(r.Status))
            .OrderByDescending(r => r.RequestedAt).FirstOrDefaultAsync(ct);
        return ride is null ? NoContent() : await Dto(ride, ct);
    }

    /// <summary>Poll this to follow a request: Requested → Accepted, or Declined / Expired / Cancelled.</summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<RideDto>> Get(Guid id, CancellationToken ct)
    {
        if (await Load(id, ct) is not { } ride) return NotFound();
        if (ride.ExpireIfStale(clock.GetUtcNow(), rideOptions.OfferTimeout)) await db.SaveChangesAsync(ct);
        return await Dto(ride, ct);
    }

    /// <summary>The road lines for the map: pickup to drop-off, and the driver's way to the pickup while they are on it.</summary>
    [HttpGet("{id:guid}/route")]
    public async Task<ActionResult<RideRouteDto>> GetRoute(Guid id, CancellationToken ct)
    {
        if (await Load(id, ct) is not { } ride) return NotFound();
        var driver = ride.DriverId is { } driverId ? await db.Drivers.FirstOrDefaultAsync(d => d.Id == driverId, ct) : null;
        return await RideRouteBuilder.BuildAsync(routes, ride, driver, ct);
    }

    /// <summary>After the trip: 1 to 5 stars for the driver and an optional comment. Once only.</summary>
    [HttpPost("{id:guid}/rating")]
    public async Task<ActionResult<RideDto>> Rate(Guid id, RatingRequest req, CancellationToken ct)
    {
        if (await Load(id, ct) is not { } ride) return NotFound();
        try
        {
            ride.RateDriver(req.Stars, req.Comment, clock.GetUtcNow());
        }
        catch (InvalidOperationException ex)
        {
            return Problem(detail: ex.Message, statusCode: 409);
        }
        catch (ArgumentOutOfRangeException ex)
        {
            return Problem(detail: ex.Message, statusCode: 400);
        }

        await db.SaveChangesAsync(ct);
        return await Dto(ride, ct);
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<ActionResult<RideDto>> Cancel(Guid id, CancellationToken ct)
    {
        if (await Load(id, ct) is not { } ride) return NotFound();
        if (ride.ExpireIfStale(clock.GetUtcNow(), rideOptions.OfferTimeout)) await db.SaveChangesAsync(ct);
        try
        {
            ride.Cancel(byDriver: false, clock.GetUtcNow());
        }
        catch (InvalidOperationException ex)
        {
            return Problem(detail: ex.Message, statusCode: 409);
        }

        await db.SaveChangesAsync(ct);
        return await Dto(ride, ct);
    }

    private async Task<Guid?> CurrentRiderId(CancellationToken ct) =>
        await db.Riders.Where(r => r.Phone == Phone).Select(r => (Guid?)r.Id).FirstOrDefaultAsync(ct);

    /// <summary>The ride, only if it belongs to the logged-in rider.</summary>
    private async Task<Ride?> Load(Guid id, CancellationToken ct) =>
        await CurrentRiderId(ct) is { } riderId
            ? await db.Rides.FirstOrDefaultAsync(r => r.Id == id && r.RiderId == riderId, ct)
            : null;

    private async Task<RideDto> Dto(Ride ride, CancellationToken ct)
    {
        var driver = ride.DriverId is { } id
            ? await db.Drivers.Include(d => d.Vehicle).FirstOrDefaultAsync(d => d.Id == id, ct)
            : null;
        var rating = ride.DriverId is { } driverId ? await RatingQueries.ForDriverAsync(db, driverId, ct) : null;
        return RideDto.From(ride, driver, rideOptions.OfferTimeout, rating);
    }

    private async Task ExpireStale(IQueryable<Ride> requested, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var changed = false;
        foreach (var r in await requested.ToListAsync(ct))
            changed |= r.ExpireIfStale(now, rideOptions.OfferTimeout);
        if (changed) await db.SaveChangesAsync(ct);
    }
}
