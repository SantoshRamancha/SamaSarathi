using System.Globalization;

namespace Samasarathi.Domain.Pricing;

/// <summary>"PeakHours" config section: when traffic is usually heavy in the city.</summary>
public sealed class PeakHourOptions
{
    /// <summary>The city's time zone. Windows PCs know "India Standard Time"; Linux and macOS know "Asia/Kolkata". Either works here.</summary>
    public string TimeZone { get; init; } = "Asia/Kolkata";

    /// <summary>Local "HH:mm-HH:mm" windows. A window may cross midnight ("22:00-02:00").</summary>
    public string[] Windows { get; init; } = ["08:00-11:00", "17:00-21:00"];

    /// <summary>Peak hours apply Monday to Friday only.</summary>
    public bool WeekdaysOnly { get; init; } = true;
}

/// <summary>
/// How much slower than free-flow a trip is expected to be. Until a live-traffic provider is connected this is a
/// fixed, visible rule of thumb: the route's free-flow time is multiplied by these when it applies.
/// </summary>
public sealed record TrafficOptions(decimal PeakSlowdown = 1.5m, decimal RainSlowdown = 1.3m);

/// <summary>Says whether "now" falls in the city's usual peak-traffic windows.</summary>
public sealed class PeakHourPolicy
{
    private readonly (TimeSpan Start, TimeSpan End)[] _windows;
    private readonly TimeZoneInfo _zone;
    private readonly bool _weekdaysOnly;

    public PeakHourPolicy(PeakHourOptions options)
    {
        _weekdaysOnly = options.WeekdaysOnly;
        _zone = FindZone(options.TimeZone);
        _windows = options.Windows.Select(Parse).ToArray();
    }

    public bool IsPeak(DateTimeOffset now)
    {
        var local = TimeZoneInfo.ConvertTime(now, _zone);
        if (_weekdaysOnly && local.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) return false;

        var t = local.TimeOfDay;
        foreach (var (start, end) in _windows)
        {
            // end < start means the window wraps past midnight.
            if (start <= end ? t >= start && t < end : t >= start || t < end) return true;
        }

        return false;
    }

    private static (TimeSpan, TimeSpan) Parse(string window)
    {
        var parts = window.Split('-');
        if (parts.Length != 2
            || !TimeSpan.TryParseExact(parts[0].Trim(), @"hh\:mm", CultureInfo.InvariantCulture, out var start)
            || !TimeSpan.TryParseExact(parts[1].Trim(), @"hh\:mm", CultureInfo.InvariantCulture, out var end))
            throw new InvalidOperationException($"PeakHours window '{window}' must look like 08:00-11:00.");
        return (start, end);
    }

    private static TimeZoneInfo FindZone(string id)
    {
        foreach (var candidate in new[] { id, "India Standard Time", "Asia/Kolkata" })
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(candidate); }
            catch (TimeZoneNotFoundException) { }
            catch (InvalidTimeZoneException) { }
        }

        // No time-zone database at all (a minimal container): India has no daylight saving, so a fixed offset is exact.
        return TimeZoneInfo.CreateCustomTimeZone("IST", TimeSpan.FromMinutes(330), "IST", "IST");
    }
}
