using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Samasarathi.Application.Abstractions;
using Samasarathi.Domain;
using Samasarathi.Infrastructure.Auth;
using Samasarathi.Infrastructure.Subscriptions;
using Samasarathi.Infrastructure.Locations;
using Samasarathi.Infrastructure.Places;
using Samasarathi.Infrastructure.Trips;
using Samasarathi.Infrastructure.Persistence;
using Samasarathi.Infrastructure.Storage;
using StackExchange.Redis;

namespace Samasarathi.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services, IConfiguration config, string contentRootPath)
    {
        services.AddDbContext<AppDbContext>(o =>
            o.UseNpgsql(config.GetConnectionString("Postgres"), npgsql => npgsql.UseNetTopologySuite()));

        // AbortOnConnectFail=false: the API still starts if Redis is briefly down, and reconnects later.
        services.AddSingleton<IConnectionMultiplexer>(_ =>
        {
            var options = ConfigurationOptions.Parse(config.GetConnectionString("Redis") ?? "localhost:6379");
            options.AbortOnConnectFail = false;
            return ConnectionMultiplexer.Connect(options);
        });
        services.AddSingleton(config.GetSection("Location").Get<LocationOptions>() ?? new LocationOptions());
        services.AddSingleton<IDriverLocationStore, RedisDriverLocationStore>();
        services.AddSingleton(config.GetSection("Rides").Get<RideOptions>() ?? new RideOptions());

        // Driver subscription: price, trial, grace days... all editable in the "Subscription" config section.
        var subscriptionOptions = config.GetSection("Subscription").Get<SubscriptionOptions>() ?? new SubscriptionOptions();
        services.AddSingleton(subscriptionOptions);
        services.AddSingleton<SubscriptionExpiryService>();

        // Who riders and drivers can contact: the "Support" config section.
        services.AddSingleton(config.GetSection("Support").Get<SupportOptions>() ?? new SupportOptions());
        services.AddHostedService(sp => sp.GetRequiredService<SubscriptionExpiryService>());

        // Destination search. To change provider, write another IPlaceSearch and register it here.
        var placeOptions = config.GetSection("PlaceSearch").Get<PlaceSearchOptions>() ?? new PlaceSearchOptions();
        services.AddSingleton(placeOptions);
        services.AddSingleton<IPlaceSearch>(sp => new NominatimPlaceSearch(
            new HttpClient { Timeout = TimeSpan.FromSeconds(8) }, placeOptions, sp.GetRequiredService<TimeProvider>()));

        // Road routes and rain for trip pricing. Each can be swapped by writing another IRouteService / IWeatherService.
        var routingOptions = config.GetSection("Routing").Get<RoutingOptions>() ?? new RoutingOptions();
        services.AddSingleton(routingOptions);
        services.AddSingleton<IRouteService>(sp => new OsrmRouteService(
            new HttpClient(), routingOptions, sp.GetRequiredService<TimeProvider>(),
            sp.GetRequiredService<ILoggerFactory>().CreateLogger<OsrmRouteService>()));

        var weatherOptions = config.GetSection("Weather").Get<WeatherOptions>() ?? new WeatherOptions();
        services.AddSingleton(weatherOptions);
        services.AddSingleton<IWeatherService>(sp => new OpenMeteoWeatherService(
            new HttpClient(), weatherOptions, sp.GetRequiredService<TimeProvider>(),
            sp.GetRequiredService<ILoggerFactory>().CreateLogger<OpenMeteoWeatherService>()));

        services.AddSingleton(config.GetSection("Otp").Get<OtpOptions>() ?? new OtpOptions());
        services.AddSingleton<IOtpService, RedisOtpService>();
        services.AddSingleton<ISmsSender, LoggingSmsSender>();

        var jwt = config.GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions();
        if (jwt.Key.Length < 32)
            throw new InvalidOperationException("Jwt:Key must be set and at least 32 characters.");
        services.AddSingleton(jwt);
        services.AddSingleton(config.GetSection("Admin").Get<AdminOptions>() ?? new AdminOptions());
        services.AddSingleton<ITokenService, JwtTokenService>();

        var storagePath = Path.Combine(contentRootPath, config["Storage:LocalPath"] ?? "uploads");
        services.AddSingleton<IFileStorage>(new LocalFileStorage(storagePath));

        return services;
    }
}
