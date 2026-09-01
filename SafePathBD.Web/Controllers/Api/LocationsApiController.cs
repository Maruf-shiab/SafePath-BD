using Microsoft.AspNetCore.Mvc;
using SafePathBD.Web.Common;
using SafePathBD.Web.Integrations.Geocoding;
using SafePathBD.Web.Services.Interfaces;

namespace SafePathBD.Web.Controllers.Api;

[ApiController]
[Route("api/v1/locations")]
public class LocationsApiController : ControllerBase
{
    private const int MaxQueryLength = 160;

    private readonly IGeocodingService _geocoding;

    public LocationsApiController(IGeocodingService geocoding)
    {
        _geocoding = geocoding;
    }

    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] string? q, [FromQuery] int limit = 6, CancellationToken cancellationToken = default)
    {
        var query = q?.Trim();

        if (string.IsNullOrEmpty(query) || query.Length < 2)
        {
            return BadRequest(ApiResult.Fail("Enter at least two characters to search."));
        }

        if (query.Length > MaxQueryLength)
        {
            return BadRequest(ApiResult.Fail("The search text is too long."));
        }

        try
        {
            var results = await _geocoding.SearchAsync(query, limit, cancellationToken);
            return Ok(ApiResult.Ok(results));
        }
        catch (GeocodingUnavailableException ex)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, ApiResult.Fail(ex.Message));
        }
    }

    [HttpGet("reverse")]
    public async Task<IActionResult> Reverse([FromQuery] double lat, [FromQuery] double lng, CancellationToken cancellationToken = default)
    {
        if (!GeoMath.IsValidCoordinate(lat, lng))
        {
            return BadRequest(ApiResult.Fail("Latitude must be between -90 and 90, and longitude between -180 and 180."));
        }

        try
        {
            var place = await _geocoding.ReverseAsync(lat, lng, cancellationToken);

            return place is null
                ? NotFound(ApiResult.Fail("No address was found for those coordinates."))
                : Ok(ApiResult.Ok(place));
        }
        catch (GeocodingUnavailableException ex)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, ApiResult.Fail(ex.Message));
        }
    }
}
