using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Samasarathi.Domain.Pricing;

namespace Samasarathi.Api.Controllers;

public sealed record FareEstimateRequest(
    [Range(0.01, 1000)] decimal RatePerKm,
    [Range(0, 500)] decimal DistanceKm,
    [Range(0, 1440)] decimal ExpectedMinutes,
    bool IsPeakHour,
    bool IsRaining,
    [Range(0.1, 10)] decimal TrafficRatio = 1m);

[ApiController]
[Route("api/fares")]
public class FaresController(FareCalculator calculator) : ControllerBase
{
    /// <summary>
    /// Upfront fare for one driver's rate. Later the server will fetch distance, minutes,
    /// traffic and weather itself; for now the caller passes them in to try the pricing rules.
    /// </summary>
    [HttpPost("estimate")]
    public ActionResult<FareQuote> Estimate(FareEstimateRequest req) =>
        calculator.Estimate(req.RatePerKm, req.DistanceKm, req.ExpectedMinutes,
            new TripConditions(req.IsPeakHour, req.IsRaining, req.TrafficRatio));
}
