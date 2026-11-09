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

/// <summary>Our team's view of driver subscriptions: who is paid up, who is about to lapse, and a way to give extra days.</summary>
[ApiController]
[Route("api/admin/subscriptions")]
[Authorize(Roles = AppRoles.Admin)]
public class AdminSubscriptionsController(AppDbContext db, SubscriptionOptions options, TimeProvider clock) : ControllerBase
{
    private const int MaxResults = 500;

    private string Admin => User.FindFirst("phone")!.Value;

    /// <summary>The numbers the app is running with: price, trial days, grace days.</summary>
    [HttpGet("settings")]
    public AdminSubscriptionSettings Settings() => new(
        options.Enabled, options.PlanName, options.MonthlyPrice, options.PlanDays, options.TrialDays, options.GraceDays,
        options.ExpiryWarningDays, options.PaymentsEnabled);

    [HttpGet("counts")]
    public async Task<AdminSubscriptionCounts> Counts(CancellationToken ct)
    {
        var rows = (await Approved().ToListAsync(ct)).Select(d => Row(d, clock.GetUtcNow())).ToList();
        return new AdminSubscriptionCounts(
            Active: rows.Count(r => r.Phase == SubscriptionPhase.Active),
            Trial: rows.Count(r => r.Phase == SubscriptionPhase.Active && r.IsTrial),
            ExpiringSoon: rows.Count(r => Matches(r, AdminSubscriptionFilter.ExpiringSoon)),
            Grace: rows.Count(r => r.Phase == SubscriptionPhase.Grace),
            Expired: rows.Count(r => r.Phase == SubscriptionPhase.Expired),
            None: rows.Count(r => r.Phase == SubscriptionPhase.None));
    }

    /// <summary>Approved drivers and where their subscription stands. Soonest to lapse first.</summary>
    /// <param name="search">Part of a name, phone number or vehicle number.</param>
    [HttpGet]
    public async Task<IReadOnlyList<AdminSubscriptionSummary>> List(
        AdminSubscriptionFilter filter = AdminSubscriptionFilter.ExpiringSoon, string? search = null, CancellationToken ct = default)
    {
        var now = clock.GetUtcNow();
        var query = Approved();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var text = $"%{search.Trim()}%";
            var compact = $"%{new string(search.Where(char.IsAsciiLetterOrDigit).ToArray()).ToUpperInvariant()}%";
            query = query.Where(d => EF.Functions.ILike(d.FullName, text)
                || EF.Functions.Like(d.Phone, compact)
                || (d.Vehicle != null && EF.Functions.Like(d.Vehicle.RegistrationNumber, compact)));
        }

        return (await query.ToListAsync(ct))
            .Select(d => Row(d, now))
            .Where(r => string.IsNullOrWhiteSpace(search) ? Matches(r, filter) : true) // a search looks across every state
            .OrderBy(r => r.ValidUntil ?? DateTimeOffset.MinValue)
            .Take(MaxResults)
            .ToList();
    }

    /// <summary>
    /// Gives a driver extra days (support case, goodwill, payment sorted out by hand). If they are still covered the days
    /// are added after the current end. A note is required so there is always a reason on record.
    /// </summary>
    [HttpPost("{driverId:guid}/grant")]
    public async Task<ActionResult<AdminSubscriptionSummary>> Grant(Guid driverId, GrantSubscriptionRequest req, CancellationToken ct)
    {
        var driver = await Approved().FirstOrDefaultAsync(d => d.Id == driverId, ct);
        if (driver is null) return Problem(detail: "Only approved drivers can have a subscription.", statusCode: 404);

        var now = clock.GetUtcNow();
        driver.AddSubscription(SubscriptionKind.Granted, options.PlanName, 0m, req.Days, now, req.Note.Trim(), Admin);
        await db.SaveChangesAsync(ct);
        return Row(driver, now);
    }

    private IQueryable<Driver> Approved() =>
        db.Drivers.Include(d => d.Vehicle).Include(d => d.Subscriptions)
            .Where(d => d.VerificationStatus == VerificationStatus.Approved).AsSplitQuery();

    private AdminSubscriptionSummary Row(Driver d, DateTimeOffset now)
    {
        var state = d.SubscriptionAt(now, options);
        var last = d.Subscriptions.OrderByDescending(s => s.EndsAt).FirstOrDefault();
        return new AdminSubscriptionSummary(
            d.Id, d.FullName, d.Phone, d.Vehicle?.RegistrationNumber, state.Phase, state.IsTrial, state.ValidUntil,
            state.DaysLeft(now), state.ExpiringSoon, last?.Kind, last?.Note);
    }

    private static bool Matches(AdminSubscriptionSummary r, AdminSubscriptionFilter f) => f switch
    {
        AdminSubscriptionFilter.ExpiringSoon => r.ExpiringSoon && r.Phase is SubscriptionPhase.Active or SubscriptionPhase.Grace,
        AdminSubscriptionFilter.Active => r.Phase == SubscriptionPhase.Active,
        AdminSubscriptionFilter.Trial => r.Phase == SubscriptionPhase.Active && r.IsTrial,
        AdminSubscriptionFilter.Grace => r.Phase == SubscriptionPhase.Grace,
        AdminSubscriptionFilter.Expired => r.Phase == SubscriptionPhase.Expired,
        AdminSubscriptionFilter.None => r.Phase == SubscriptionPhase.None,
        _ => true,
    };
}
