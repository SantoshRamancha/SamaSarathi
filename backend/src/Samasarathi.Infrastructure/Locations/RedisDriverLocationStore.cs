using Samasarathi.Application.Abstractions;
using StackExchange.Redis;

namespace Samasarathi.Infrastructure.Locations;

/// <summary>
/// Stores online drivers in a Redis geo set, so "drivers within 3 km" is one fast query.
/// A second set records when each driver last reported; drivers who went quiet are removed before searching.
/// </summary>
public sealed class RedisDriverLocationStore(IConnectionMultiplexer redis, LocationOptions options) : IDriverLocationStore
{
    private const string GeoKey = "drivers:online";
    private const string LastSeenKey = "drivers:lastseen";
    private IDatabase Db => redis.GetDatabase();

    public async Task UpdateAsync(Guid driverId, double latitude, double longitude, DateTimeOffset at, CancellationToken ct = default)
    {
        var member = driverId.ToString();
        var tx = Db.CreateTransaction();
        _ = tx.GeoAddAsync(GeoKey, longitude, latitude, member);
        _ = tx.SortedSetAddAsync(LastSeenKey, member, at.ToUnixTimeSeconds());
        await tx.ExecuteAsync();
    }

    public async Task RemoveAsync(Guid driverId, CancellationToken ct = default)
    {
        var member = driverId.ToString();
        var tx = Db.CreateTransaction();
        _ = tx.GeoRemoveAsync(GeoKey, member);
        _ = tx.SortedSetRemoveAsync(LastSeenKey, member);
        await tx.ExecuteAsync();
    }

    public async Task<IReadOnlyList<NearbyDriver>> FindNearbyAsync(
        double latitude, double longitude, double radiusKm, int limit, DateTimeOffset now, CancellationToken ct = default)
    {
        await RemoveStaleAsync(now);

        var results = await Db.GeoSearchAsync(GeoKey, longitude, latitude, new GeoSearchCircle(radiusKm, GeoUnit.Kilometers),
            count: limit, order: Order.Ascending, options: GeoRadiusOptions.WithDistance | GeoRadiusOptions.WithCoordinates);

        return results
            .Where(r => r.Position is not null)
            .Select(r => new NearbyDriver(
                Guid.Parse(r.Member.ToString()), r.Position!.Value.Latitude, r.Position.Value.Longitude, r.Distance ?? 0))
            .ToList();
    }

    private async Task RemoveStaleAsync(DateTimeOffset now)
    {
        var cutoff = now.AddSeconds(-options.StaleAfterSeconds).ToUnixTimeSeconds();
        var stale = await Db.SortedSetRangeByScoreAsync(LastSeenKey, double.NegativeInfinity, cutoff);
        if (stale.Length == 0) return;

        // A geo set is a sorted set underneath, so ZREM removes several members at once.
        var tx = Db.CreateTransaction();
        _ = tx.SortedSetRemoveAsync(GeoKey, stale);
        _ = tx.SortedSetRemoveAsync(LastSeenKey, stale);
        await tx.ExecuteAsync();
    }
}
