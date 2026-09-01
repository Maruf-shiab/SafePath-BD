using Microsoft.AspNetCore.Mvc;
using SafePathBD.Web.Common;
using SafePathBD.Web.Services.Implementations;
using SafePathBD.Web.Services.Interfaces;

namespace SafePathBD.Web.Controllers.Api;

[ApiController]
[Route("api/v1/map")]
public class MapReportsApiController : ControllerBase
{
    private readonly IReportService _reportService;

    public MapReportsApiController(IReportService reportService)
    {
        _reportService = reportService;
    }

    /// <summary>Verified, public report markers inside the current map bounds.</summary>
    [HttpGet("reports")]
    public async Task<IActionResult> Reports(
        [FromQuery] double minLat,
        [FromQuery] double minLng,
        [FromQuery] double maxLat,
        [FromQuery] double maxLng,
        [FromQuery] string? type = null,
        [FromQuery] int limit = 200,
        CancellationToken cancellationToken = default)
    {
        var bounds = new MapBounds(minLat, minLng, maxLat, maxLng);
        if (!bounds.IsValid)
        {
            return BadRequest(ApiResult.Fail("The requested map area is not valid."));
        }

        var normalizedType = type?.ToUpperInvariant() switch
        {
            ReportTypes.Accident => ReportTypes.Accident,
            ReportTypes.Hazard => ReportTypes.Hazard,
            null or "" => null,
            _ => "INVALID"
        };

        if (normalizedType == "INVALID")
        {
            return BadRequest(ApiResult.Fail("Report type must be ACCIDENT or HAZARD."));
        }

        if (limit is <= 0 or > ReportService.MaxMapReports)
        {
            return BadRequest(ApiResult.Fail($"Limit must be between 1 and {ReportService.MaxMapReports}."));
        }

        var reports = await _reportService.GetPublicMapReportsAsync(bounds, normalizedType, limit, cancellationToken);
        return Ok(ApiResult.Ok(reports));
    }
}
