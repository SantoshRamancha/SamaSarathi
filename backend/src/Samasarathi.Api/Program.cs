using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Samasarathi.Application;
using Samasarathi.Domain.Pricing;
using Samasarathi.Infrastructure;
using Samasarathi.Infrastructure.Auth;
using Scalar.AspNetCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, cfg) => cfg.ReadFrom.Configuration(ctx.Configuration).WriteTo.Console());

builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddOpenApi();
// Flutter web runs on a random localhost port during development.
builder.Services.AddCors(o => o.AddPolicy("dev", p =>
    p.SetIsOriginAllowed(origin => new Uri(origin).IsLoopback).AllowAnyHeader().AllowAnyMethod()));
builder.Services.AddInfrastructure(builder.Configuration, builder.Environment.ContentRootPath);
builder.Services.AddSingleton(TimeProvider.System);

// Every request with "Authorization: Bearer <token>" is checked here; [Authorize(Roles=...)] then uses the "role" claim.
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<JwtOptions>((o, jwt) =>
    {
        // Keep claim names exactly as issued ("phone", "role") instead of renaming them to long XML URIs.
        o.MapInboundClaims = false;
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = jwt.SigningKey(),
            NameClaimType = "phone",
            RoleClaimType = "role",
        };
    });
builder.Services.AddAuthorization();
var pricing = builder.Configuration.GetSection("Pricing").Get<PricingOptions>() ?? new PricingOptions();
var fareCalculator = new FareCalculator(pricing);
builder.Services.AddSingleton(fareCalculator);
var traffic = builder.Configuration.GetSection("Traffic").Get<TrafficOptions>() ?? new TrafficOptions();
var peakHours = new PeakHourPolicy(builder.Configuration.GetSection("PeakHours").Get<PeakHourOptions>() ?? new PeakHourOptions());
builder.Services.AddSingleton(peakHours);
builder.Services.AddSingleton(new RideEstimator(fareCalculator, pricing, traffic));
builder.Services.AddSingleton<TripPlanner>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    // Interactive API docs at /scalar; the bare root redirects there so the browser lands somewhere useful.
    app.MapScalarApiReference();
    app.MapGet("/", () => Results.Redirect("/scalar")).ExcludeFromDescription();
    app.UseCors("dev");
}

app.UseSerilogRequestLogging();
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapControllers();

app.Run();
