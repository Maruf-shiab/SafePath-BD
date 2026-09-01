using Microsoft.AspNetCore.Mvc;
using SafePathBD.Web.Common;
using SafePathBD.Web.Services.Implementations;
using SafePathBD.Web.Services.Interfaces;

namespace SafePathBD.Web.Controllers.Api;

[ApiController]
[Route("api/v1/emergency-services")]
public class EmergencyServicesApiController : ControllerBase
{
    private readonly IEmergencyService _emergencyService;

    public EmergencyServicesApiController(IEmergencyService emergencyService)
    {
        _emergencyService = emergencyService;
    }

    [HttpGet("types")]
    public async Task<IActionResult> Types(CancellationToken cancellationToken)
    {
        var types = await _emergencyService.GetServiceTypesAsync(cancellationToken);
        return Ok(ApiResult.Ok(types));
    }

    [HttpGet("nearby")]
    public async Task<IActionResult> Nearby(
        [FromQuery] double lat,
        [FromQuery] double lng,
        [FromQuery] string? type = null,
        [FromQuery] double radiusKm = 15,
        [FromQuery] int limit = 25,
        CancellationToken cancellationToken = default)
    {
        if (!GeoMath.IsValidCoordinate(lat, lng))
        {
            return BadRequest(ApiResult.Fail("Latitude must be between -90 and 90, and longitude between -180 and 180."));
        }

        if (radiusKm is <= 0 or > EmergencyService.MaxRadiusKm)
        {
            return BadRequest(ApiResult.Fail($"Search radius must be between 0 and {EmergencyService.MaxRadiusKm} km."));
        }

        if (limit is <= 0 or > EmergencyService.MaxLimit)
        {
            return BadRequest(ApiResult.Fail($"Limit must be between 1 and {EmergencyService.MaxLimit}."));
        }

        var results = await _emergencyService.GetNearbyAsync(lat, lng, type, radiusKm, limit, cancellationToken);
        return Ok(ApiResult.Ok(results));
    }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Details(long id, CancellationToken cancellationToken)
    {
        if (id <= 0)
        {
            return BadRequest(ApiResult.Fail("Invalid emergency service id."));
        }

        var service = await _emergencyService.GetByIdAsync((ulong)id, cancellationToken);

        return service is null
            ? NotFound(ApiResult.Fail("Emergency service not found."))
            : Ok(ApiResult.Ok(service));
    }
}
