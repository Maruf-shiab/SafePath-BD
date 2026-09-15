using Microsoft.AspNetCore.Mvc;
using SafePathBD.Web.Common;
using SafePathBD.Web.Models.DTOs.IntelligentRouting;
using SafePathBD.Web.Security;
using SafePathBD.Web.Services.Interfaces;

namespace SafePathBD.Web.Controllers.Api;

[ApiController]
[Route("api/v1/routes/intelligent")]
public sealed class IntelligentRoutingApiController : ControllerBase
{
    private readonly IIntelligentRoutingService _service;

    public IntelligentRoutingApiController(IIntelligentRoutingService service)
    {
        _service = service;
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(32 * 1024)]
    public async Task<IActionResult> Search(
        [FromBody] IntelligentRouteRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _service.SearchAsync(request, CurrentUserId(), cancellationToken);
        return result.Status switch
        {
            IntelligentRouteStatuses.Success => Ok(ApiResult.Ok(result.Response!)),
            IntelligentRouteStatuses.Invalid => BadRequest(ApiResult.Fail(result.Message ?? "The intelligent route request is invalid.")),
            IntelligentRouteStatuses.NoRoute => UnprocessableEntity(ApiResult.Fail(result.Message ?? "No practical journey was found.")),
            IntelligentRouteStatuses.ProviderUnavailable => StatusCode(StatusCodes.Status503ServiceUnavailable, ApiResult.Fail(result.Message ?? "Road routing is temporarily unavailable.")),
            _ => StatusCode(StatusCodes.Status500InternalServerError, ApiResult.Fail("The intelligent route request could not be completed."))
        };
    }

    [HttpPost("what-if")]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(12 * 1024)]
    public async Task<IActionResult> WhatIf(
        [FromBody] WhatIfDisruptionRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _service.SimulateDisruptionAsync(request, CurrentUserId(), cancellationToken);
        return result.Status switch
        {
            IntelligentRouteStatuses.Success => Ok(ApiResult.Ok(result.Response!)),
            IntelligentRouteStatuses.Invalid => BadRequest(ApiResult.Fail(result.Message ?? "The what-if request is invalid.")),
            IntelligentRouteStatuses.NoRoute => UnprocessableEntity(ApiResult.Ok(result.Response!, result.Message)),
            IntelligentRouteStatuses.SearchExpired => StatusCode(StatusCodes.Status410Gone, ApiResult.Fail(result.Message ?? "This intelligent route search has expired.")),
            IntelligentRouteStatuses.Forbidden => StatusCode(StatusCodes.Status403Forbidden, ApiResult.Fail(result.Message ?? "This route search is not available to this session.")),
            _ => StatusCode(StatusCodes.Status500InternalServerError, ApiResult.Fail("The what-if simulation could not be completed."))
        };
    }

    private ulong? CurrentUserId() => User.Identity?.IsAuthenticated == true ? User.GetUserId() : null;
}
