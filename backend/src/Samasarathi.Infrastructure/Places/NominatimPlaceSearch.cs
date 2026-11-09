using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;
using Samasarathi.Application.Abstractions;

namespace Samasarathi.Infrastructure.Places;

/// <summary>
/// Place search on OpenStreetMap's Nominatim. It sees only the typed text and the rider's rounded position
/// (used to prefer nearby places), never who is asking. Swapping to Google Places or Ola Maps later means
/// writing another <see cref="IPlaceSearch"/> and changing one line in DependencyInjection.
/// </summary>
public sealed class NominatimPlaceSearch : IPlaceSearch
{
    private static readonly TimeSpan CacheFor = TimeSpan.FromMinutes(10);
    private const int MaxCached = 500;

    private readonly HttpClient _http;
    private readonly PlaceSearchOptions _options;
    private readonly TimeProvider _clock;

    // Nominatim allows 1 request per second in total, so outgoing calls go through one gate.
    private readonly SemaphoreSlim _gate = new(1, 1);
    private DateTimeOffset _lastCall = DateTimeOffset.MinValue;
    private readonly ConcurrentDictionary<string, (DateTimeOffset At, IReadOnlyList<PlaceResult> Results)> _cache = new();

    public NominatimPlaceSearch(HttpClient http, PlaceSearchOptions options, TimeProvider clock)
    {
        _http = http;
        _options = options;
        _clock = clock;
    }

    public async Task<IReadOnlyList<PlaceResult>> SearchAsync(
        string query, double? nearLatitude, double? nearLongitude, CancellationToken ct = default)
    {
        query = query.Trim();
        if (query.Length < 2) return [];

        // Rounding the position to ~1 km makes repeat searches from the same area share a cache entry.
        var key = $"{query.ToLowerInvariant()}|{nearLatitude:0.0#}|{nearLongitude:0.0#}";
        if (_cache.TryGetValue(key, out var hit) && _clock.GetUtcNow() - hit.At < CacheFor)
            return hit.Results;

        var results = await Fetch(query, nearLatitude, nearLongitude, ct);

        if (_cache.Count >= MaxCached) _cache.Clear();
        _cache[key] = (_clock.GetUtcNow(), results);
        return results;
    }

    private async Task<IReadOnlyList<PlaceResult>> Fetch(string query, double? lat, double? lng, CancellationToken ct)
    {
        var url = BuildUrl(query, lat, lng);

        await _gate.WaitAsync(ct);
        try
        {
            var wait = TimeSpan.FromMilliseconds(_options.MinMillisecondsBetweenRequests) - (_clock.GetUtcNow() - _lastCall);
            if (wait > TimeSpan.Zero) await Task.Delay(wait, ct);

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.UserAgent.ParseAdd(_options.UserAgent);
                using var response = await _http.SendAsync(request, ct);
                _lastCall = _clock.GetUtcNow();
                if (!response.IsSuccessStatusCode)
                    throw new PlaceSearchUnavailableException($"Place search answered {(int)response.StatusCode}.");

                return Parse(await response.Content.ReadAsStringAsync(ct), _options.MaxResults);
            }
            catch (HttpRequestException ex)
            {
                throw new PlaceSearchUnavailableException("Place search could not be reached.", ex);
            }
            catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
            {
                throw new PlaceSearchUnavailableException("Place search timed out.", ex);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public string BuildUrl(string query, double? lat, double? lng)
    {
        var inv = CultureInfo.InvariantCulture;
        var url = $"{_options.BaseUrl.TrimEnd('/')}/search?format=jsonv2&addressdetails=0"
                  + $"&limit={_options.MaxResults}&countrycodes={Uri.EscapeDataString(_options.CountryCodes)}"
                  + $"&q={Uri.EscapeDataString(query)}";

        if (lat is { } la && lng is { } ln)
        {
            // viewbox = left,top,right,bottom (longitude first). Not bounded: it only ranks nearby places higher.
            var d = _options.NearbyBoxDegrees;
            url += $"&viewbox={(ln - d).ToString(inv)},{(la + d).ToString(inv)},{(ln + d).ToString(inv)},{(la - d).ToString(inv)}";
        }

        return url;
    }

    /// <summary>Reads Nominatim's JSON array. Entries without usable coordinates are skipped.</summary>
    public static IReadOnlyList<PlaceResult> Parse(string json, int max)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array) return [];

            var results = new List<PlaceResult>();
            foreach (var item in doc.RootElement.EnumerateArray())
            {
                if (!TryNumber(item, "lat", out var lat) || !TryNumber(item, "lon", out var lon)) continue;
                if (lat is < -90 or > 90 || lon is < -180 or > 180) continue;

                var address = item.TryGetProperty("display_name", out var dn) ? dn.GetString() ?? "" : "";
                var name = item.TryGetProperty("name", out var n) ? n.GetString() : null;
                // Nominatim leaves "name" empty for plain addresses: use the first part of the full address instead.
                if (string.IsNullOrWhiteSpace(name)) name = address.Split(',')[0].Trim();
                if (string.IsNullOrWhiteSpace(name)) continue;

                results.Add(new PlaceResult(name, address, lat, lon));
                if (results.Count == max) break;
            }

            return results;
        }
        catch (JsonException ex)
        {
            throw new PlaceSearchUnavailableException("Place search gave an unreadable answer.", ex);
        }
    }

    // Nominatim sends coordinates as strings ("17.44"); accept numbers too in case that changes.
    private static bool TryNumber(JsonElement item, string name, out double value)
    {
        value = 0;
        if (!item.TryGetProperty(name, out var p)) return false;
        return p.ValueKind switch
        {
            JsonValueKind.String => double.TryParse(p.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out value),
            JsonValueKind.Number => p.TryGetDouble(out value),
            _ => false,
        };
    }
}
