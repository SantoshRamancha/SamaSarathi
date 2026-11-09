using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Samasarathi.Application.Abstractions;

namespace Samasarathi.Api.Controllers;

/// <summary>Destination search for riders.</summary>
[ApiController]
[Route("api/places")]
[Authorize(Roles = AppRoles.Rider)]
public class PlacesController(IPlaceSearch places, ILogger<PlacesController> logger) : ControllerBase
{
    /// <summary>
    /// Places matching what the rider typed, nearest to them first. Send the rider's position as
    /// <paramref name="latitude"/>/<paramref name="longitude"/> so local places rank above same-named ones elsewhere.
    /// </summary>
    [HttpGet("search")]
    public async Task<ActionResult<IReadOnlyList<PlaceResult>>> Search(
        [Required, StringLength(100, MinimumLength = 2)] string query,
        [Range(-90, 90)] double? latitude, [Range(-180, 180)] double? longitude, CancellationToken ct)
    {
        try
        {
            return Ok(await places.SearchAsync(query, latitude, longitude, ct));
        }
        catch (PlaceSearchUnavailableException ex)
        {
            logger.LogWarning(ex, "Place search failed");
            return Problem(detail: "Place search isn't available right now. You can tap the map instead.", statusCode: 502);
        }
    }
}
