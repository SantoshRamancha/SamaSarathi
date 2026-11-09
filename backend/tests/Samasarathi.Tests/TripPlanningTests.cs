using Samasarathi.Application;
using Samasarathi.Application.Abstractions;
using Samasarathi.Domain;
using Samasarathi.Domain.Pricing;
using Samasarathi.Infrastructure.Trips;

namespace Samasarathi.Tests;

public class TripPlanningTests
{
    // 2026-11-02 is a Monday. India is UTC+5:30, so 09:00 UTC = 14:30 IST (off-peak) and 04:00 UTC = 09:30 IST (peak).
    private static readonly DateTimeOffset MondayMorningPeak = new(2026, 11, 2, 4, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset MondayAfternoon = new(2026, 11, 2, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Sunday = new(2026, 11, 1, 4, 0, 0, TimeSpan.Zero);

    private const double PickupLat = 17.3616, PickupLng = 78.4747, DropLat = 17.4435, DropLng = 78.3772;

    private static readonly PricingOptions Pricing = new();
    private static readonly FareCalculator Calculator = new(Pricing);

    // --- peak hours ----------------------------------------------------------------------------

    [Theory]
    [InlineData(2026, 11, 2, 2, 29, false)] // 07:59 IST
    [InlineData(2026, 11, 2, 2, 30, true)]  // 08:00 IST, window opens
    [InlineData(2026, 11, 2, 5, 29, true)]  // 10:59 IST
    [InlineData(2026, 11, 2, 5, 30, false)] // 11:00 IST, window closes
    [InlineData(2026, 11, 2, 12, 30, true)] // 18:00 IST, evening window
    [InlineData(2026, 11, 2, 15, 30, false)]// 21:00 IST
    public void Peak_windows_follow_the_cities_local_time(int y, int mo, int d, int h, int mi, bool expected)
    {
        var policy = new PeakHourPolicy(new PeakHourOptions());
        Assert.Equal(expected, policy.IsPeak(new DateTimeOffset(y, mo, d, h, mi, 0, TimeSpan.Zero)));
    }

    [Fact]
    public void Weekends_are_not_peak_when_weekdays_only()
    {
        Assert.False(new PeakHourPolicy(new PeakHourOptions()).IsPeak(Sunday));
        Assert.True(new PeakHourPolicy(new PeakHourOptions { WeekdaysOnly = false }).IsPeak(Sunday));
    }

    [Fact]
    public void A_window_can_cross_midnight()
    {
        var policy = new PeakHourPolicy(new PeakHourOptions { Windows = ["22:00-02:00"], WeekdaysOnly = false });

        Assert.True(policy.IsPeak(new DateTimeOffset(2026, 11, 2, 17, 30, 0, TimeSpan.Zero)));  // 23:00 IST
        Assert.True(policy.IsPeak(new DateTimeOffset(2026, 11, 2, 20, 0, 0, TimeSpan.Zero)));   // 01:30 IST
        Assert.False(policy.IsPeak(new DateTimeOffset(2026, 11, 2, 9, 0, 0, TimeSpan.Zero)));   // 14:30 IST
    }

    [Fact]
    public void A_badly_written_window_is_rejected_at_startup() =>
        Assert.Throws<InvalidOperationException>(() => new PeakHourPolicy(new PeakHourOptions { Windows = ["8-11"] }));

    // --- estimate from a route -----------------------------------------------------------------

    private static RideEstimator Estimator() => new(Calculator, Pricing, new TrafficOptions(PeakSlowdown: 1.5m, RainSlowdown: 1.3m));

    [Fact]
    public void Dry_off_peak_trip_is_charged_by_distance_and_keeps_the_route_time()
    {
        var e = Estimator().EstimateRoute(10, 10.0, 20, new TripConditions(false, false));

        Assert.Equal(10.0m, e.DistanceKm);
        Assert.Equal(20m, e.Minutes);
        Assert.Equal(PricingMode.Distance, e.Quote.Mode);
        Assert.Equal(100m, e.Quote.Fare);
    }

    [Fact]
    public void Peak_hour_slows_the_expected_time_and_charges_by_time()
    {
        // 10 km at Rs10/km = 100 by distance. Free-flow 20 min x 1.5 = 30 min; time rate = 10 x 25 / 60 per min.
        var e = Estimator().EstimateRoute(10, 10.0, 20, new TripConditions(IsPeakHour: true, IsRaining: false));

        Assert.Equal(30m, e.Minutes);
        Assert.Equal(PricingMode.Time, e.Quote.Mode);
        Assert.Equal(125m, e.Quote.Fare);
    }

    [Fact]
    public void Rain_alone_also_switches_to_time_pricing()
    {
        var e = Estimator().EstimateRoute(10, 10.0, 20, new TripConditions(IsPeakHour: false, IsRaining: true));

        Assert.Equal(26m, e.Minutes); // 20 x 1.3
        Assert.Equal(PricingMode.Time, e.Quote.Mode);
    }

    [Fact]
    public void Peak_and_rain_together_slow_the_trip_by_both_factors()
    {
        var e = Estimator().EstimateRoute(10, 10.0, 20, new TripConditions(true, true));
        Assert.Equal(39m, e.Minutes); // 20 x 1.5 x 1.3
    }

    [Fact]
    public void A_time_fare_is_never_below_the_distance_fare()
    {
        // A fast route (10 km in 8 min): even slowed to 12 min the time fare is below distance, so distance wins.
        var e = Estimator().EstimateRoute(10, 10.0, 8, new TripConditions(IsPeakHour: true, IsRaining: false));

        Assert.Equal(PricingMode.Time, e.Quote.Mode);
        Assert.Equal(100m, e.Quote.Fare);
    }

    [Fact]
    public void A_route_that_is_too_short_or_too_long_is_refused()
    {
        Assert.Throws<ArgumentException>(() => Estimator().EstimateRoute(10, 0.1, 1));
        Assert.Throws<ArgumentException>(() => Estimator().EstimateRoute(10, 150, 200));
    }

    // --- the planner ---------------------------------------------------------------------------

    private sealed class FakeRoutes(RouteInfo? route) : IRouteService
    {
        public Task<RouteInfo?> TryGetRouteAsync(double a, double b, double c, double d, CancellationToken ct = default) =>
            Task.FromResult(route);
    }

    private sealed class FakeWeather(WeatherInfo? weather) : IWeatherService
    {
        public Task<WeatherInfo?> TryGetCurrentAsync(double lat, double lng, CancellationToken ct = default) =>
            Task.FromResult(weather);
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private static TripPlanner Planner(RouteInfo? route, WeatherInfo? weather, DateTimeOffset now) =>
        new(new FakeRoutes(route), new FakeWeather(weather), new PeakHourPolicy(new PeakHourOptions()), Estimator(), new FixedClock(now));

    private static readonly RouteInfo TenKm = new(10.0, 20.0, [new GeoPoint(17.36, 78.47), new GeoPoint(17.44, 78.38)]);

    [Fact]
    public async Task Planner_uses_the_road_route_when_there_is_one()
    {
        var plan = await Planner(TenKm, new WeatherInfo(false, 0), MondayAfternoon).PlanAsync(10, PickupLat, PickupLng, DropLat, DropLng);

        Assert.Equal(RouteSource.Road, plan.Source);
        Assert.Equal(10.0m, plan.Estimate.DistanceKm);
        Assert.Equal(2, plan.Route.Count);
        Assert.True(plan.WeatherKnown);
        Assert.Equal(PricingMode.Distance, plan.Estimate.Quote.Mode);
    }

    [Fact]
    public async Task Planner_prices_by_time_in_peak_hour()
    {
        var plan = await Planner(TenKm, new WeatherInfo(false, 0), MondayMorningPeak).PlanAsync(10, PickupLat, PickupLng, DropLat, DropLng);

        Assert.True(plan.Conditions.IsPeakHour);
        Assert.Equal(PricingMode.Time, plan.Estimate.Quote.Mode);
    }

    [Fact]
    public async Task Planner_prices_by_time_in_rain()
    {
        var plan = await Planner(TenKm, new WeatherInfo(true, 1.2), MondayAfternoon).PlanAsync(10, PickupLat, PickupLng, DropLat, DropLng);

        Assert.True(plan.Conditions.IsRaining);
        Assert.False(plan.Conditions.IsPeakHour);
        Assert.Equal(PricingMode.Time, plan.Estimate.Quote.Mode);
    }

    [Fact]
    public async Task Planner_falls_back_to_the_straight_line_when_routing_is_down()
    {
        var plan = await Planner(null, new WeatherInfo(false, 0), MondayAfternoon).PlanAsync(10, PickupLat, PickupLng, DropLat, DropLng);

        Assert.Equal(RouteSource.StraightLine, plan.Source);
        Assert.Empty(plan.Route);
        Assert.True(plan.Estimate.DistanceKm > 10); // ~13 km straight line x 1.3
    }

    [Fact]
    public async Task Planner_assumes_dry_weather_when_the_weather_service_is_down()
    {
        var plan = await Planner(TenKm, null, MondayAfternoon).PlanAsync(10, PickupLat, PickupLng, DropLat, DropLng);

        Assert.False(plan.WeatherKnown);
        Assert.False(plan.Conditions.IsRaining);
        Assert.Equal(PricingMode.Distance, plan.Estimate.Quote.Mode);
    }

    [Fact]
    public async Task Planner_still_refuses_a_trip_that_is_too_short()
    {
        var tiny = new RouteInfo(0.05, 1, []);
        await Assert.ThrowsAsync<ArgumentException>(() =>
            Planner(tiny, null, MondayAfternoon).PlanAsync(10, PickupLat, PickupLng, PickupLat + 0.0003, PickupLng));
    }

    // --- OSRM answer ---------------------------------------------------------------------------

    private const string OsrmOk = """
        {"code":"Ok","routes":[{"distance":12345.6,"duration":1500.0,"geometry":{"type":"LineString","coordinates":[[78.4747,17.3616],[78.4,17.4],[78.3772,17.4435]]}}],"waypoints":[]}
        """;

    [Fact]
    public void Osrm_answer_is_read_into_km_minutes_and_points()
    {
        var route = OsrmRouteService.Parse(OsrmOk)!;

        Assert.Equal(12.3456, route.DistanceKm, 4);
        Assert.Equal(25.0, route.DurationMinutes, 4);
        Assert.Equal(3, route.Geometry.Count);
        Assert.Equal(17.3616, route.Geometry[0].Latitude, 4);  // GeoJSON is [lng, lat]: we swap to lat, lng
        Assert.Equal(78.4747, route.Geometry[0].Longitude, 4);
    }

    [Theory]
    [InlineData("""{"code":"NoRoute","routes":[]}""")]
    [InlineData("""{"code":"Ok","routes":[]}""")]
    [InlineData("""{"message":"nope"}""")]
    [InlineData("not json at all")]
    public void Osrm_answers_without_a_route_give_null(string json) => Assert.Null(OsrmRouteService.Parse(json));

    // --- Open-Meteo answer ---------------------------------------------------------------------

    [Theory]
    [InlineData(0.0, 0, false)]    // dry, clear
    [InlineData(0.1, 3, false)]    // trace, overcast
    [InlineData(0.2, 3, true)]     // at the threshold
    [InlineData(1.5, 61, true)]    // rain
    [InlineData(0.0, 63, true)]    // rain code even with no measured mm yet
    [InlineData(0.0, 81, true)]    // rain showers
    [InlineData(0.0, 95, true)]    // thunderstorm
    [InlineData(0.0, 45, false)]   // fog is not rain
    [InlineData(0.0, 71, false)]   // snow is not rain here
    public void Weather_is_rain_by_amount_or_by_code(double mm, int code, bool raining)
    {
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        var json = "{\"current\":{\"precipitation\":" + mm.ToString(inv) + ",\"weather_code\":" + code + "}}";

        var weather = OpenMeteoWeatherService.Parse(json, 0.2)!;

        Assert.Equal(raining, weather.IsRaining);
    }

    [Theory]
    [InlineData("""{"hourly":{}}""")]
    [InlineData("not json")]
    public void Weather_answers_without_current_conditions_give_null(string json) =>
        Assert.Null(OpenMeteoWeatherService.Parse(json, 0.2));

    [Fact]
    public async Task Force_rain_reports_rain_without_calling_the_weather_service()
    {
        var service = new OpenMeteoWeatherService(
            new HttpClient(new ThrowingHandler()), new WeatherOptions { ForceRain = true }, TimeProvider.System,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<OpenMeteoWeatherService>.Instance);

        var weather = await service.TryGetCurrentAsync(17.4, 78.4);

        Assert.True(weather!.IsRaining);
    }

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            throw new InvalidOperationException("The weather service must not be called when ForceRain is on.");
    }
}
