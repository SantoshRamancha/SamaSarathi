using Microsoft.EntityFrameworkCore;
using Samasarathi.Infrastructure.Persistence;

namespace Samasarathi.Api.Controllers;

/// <summary>Average stars drivers have been given by riders. Worked out from the rides, so there is no counter to keep in step.</summary>
public static class RatingQueries
{
    public static async Task<Dictionary<Guid, RatingSummaryDto>> ForDriversAsync(
        AppDbContext db, IReadOnlyCollection<Guid> driverIds, CancellationToken ct)
    {
        if (driverIds.Count == 0) return [];

        var rows = await db.Rides
            .Where(r => r.DriverId != null && driverIds.Contains(r.DriverId.Value) && r.RatingForDriver != null)
            .GroupBy(r => r.DriverId!.Value)
            .Select(g => new { DriverId = g.Key, Average = g.Average(r => (double)r.RatingForDriver!.Value), Count = g.Count() })
            .ToListAsync(ct);

        return rows.ToDictionary(x => x.DriverId, x => new RatingSummaryDto(Math.Round(x.Average, 1), x.Count));
    }

    public static async Task<RatingSummaryDto> ForDriverAsync(AppDbContext db, Guid driverId, CancellationToken ct) =>
        (await ForDriversAsync(db, [driverId], ct)).GetValueOrDefault(driverId) ?? RatingSummaryDto.None;
}
