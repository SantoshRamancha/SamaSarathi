using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Samasarathi.Application.Abstractions;
using Samasarathi.Domain;
using Samasarathi.Domain.Entities;
using Samasarathi.Infrastructure.Persistence;

namespace Samasarathi.Infrastructure.Subscriptions;

/// <summary>
/// Runs in the background while the API is up. Every few minutes it (1) marks periods that have ended as Expired and
/// (2) takes drivers whose subscription (and grace period) has run out, or who were suspended, offline, so riders stop seeing them.
/// A driver in the middle of a ride is left alone and is taken offline once the ride is over.
/// </summary>
public sealed class SubscriptionExpiryService(
    IServiceScopeFactory scopes, SubscriptionOptions options, TimeProvider clock, ILogger<SubscriptionExpiryService> log)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Runs even with subscriptions switched off: it also takes suspended drivers offline.
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(Math.Max(1, options.ExpiryCheckMinutes)));
        do
        {
            try
            {
                await RunOnceAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // A database hiccup must not stop the job: the next tick tries again.
                log.LogWarning(ex, "Subscription expiry check failed");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    /// <summary>One pass. Public so it can be run from tests.</summary>
    public async Task RunOnceAsync(CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var locations = scope.ServiceProvider.GetRequiredService<IDriverLocationStore>();
        var now = clock.GetUtcNow();

        var ended = await db.Subscriptions
            .Where(s => s.Status == SubscriptionStatus.Active && s.EndsAt <= now)
            .ToListAsync(ct);
        foreach (var s in ended) s.Status = SubscriptionStatus.Expired;

        var online = await db.Drivers.Include(d => d.Subscriptions).Where(d => d.IsOnline).ToListAsync(ct);
        var lapsed = online.Where(d => !d.CanAcceptRides(now, options)).ToList();
        var lapsedIds = lapsed.Select(d => d.Id).ToList();
        var inRide = lapsedIds.Count == 0
            ? []
            : await db.Rides
                .Where(r => r.DriverId != null && lapsedIds.Contains(r.DriverId.Value) && Ride.ActiveStatuses.Contains(r.Status))
                .Select(r => r.DriverId!.Value).Distinct().ToListAsync(ct);

        var toOffline = lapsed.Where(d => !inRide.Contains(d.Id)).ToList();
        foreach (var d in toOffline) d.GoOffline();

        if (ended.Count > 0 || toOffline.Count > 0) await db.SaveChangesAsync(ct);
        foreach (var d in toOffline) await locations.RemoveAsync(d.Id, ct);

        if (toOffline.Count > 0)
            log.LogInformation("Took {Count} driver(s) offline: subscription ended", toOffline.Count);
    }
}
