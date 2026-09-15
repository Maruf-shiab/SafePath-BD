using Microsoft.AspNetCore.Mvc;
using SafePathBD.Web.Common;
using SafePathBD.Web.Models.DTOs.Routing;
using SafePathBD.Web.Security;
using SafePathBD.Web.Services.Interfaces;

namespace SafePathBD.Web.Controllers.Api;

[ApiController]
[Route("api/v1/routes")]
public sealed class RoutingApiController : ControllerBase
{
    private readonly IRoutingService _routingService;

    public RoutingApiController(IRoutingService routingService)
    {
        _routingService = routingService;
    }

    [HttpPost("search")]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(16 * 1024)]
    public async Task<IActionResult> Search(
        [FromBody] RouteSearchRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _routingService.SearchAsync(request, CurrentUserId(), cancellationToken);

        return result.Status switch
        {
            RouteSearchStatus.Success => Ok(ApiResult.Ok(result.Response!)),
            RouteSearchStatus.InvalidRequest => BadRequest(ApiResult.Fail(result.Message ?? "The route request is invalid.")),
            RouteSearchStatus.NoRoute => UnprocessableEntity(ApiResult.Fail(result.Message ?? "No drivable route was found between these locations.")),
            RouteSearchStatus.ProviderUnavailable => StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                ApiResult.Fail(result.Message ?? "The routing provider is temporarily unavailable.")),
            _ => StatusCode(StatusCodes.Status500InternalServerError, ApiResult.Fail("The route request could not be completed."))
        };
    }

    [HttpPost("recheck")]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(8 * 1024)]
    public async Task<IActionResult> Recheck(
        [FromBody] RouteRecheckRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _routingService.RecheckAsync(request, CurrentUserId(), cancellationToken);

        return result.Status switch
        {
            RouteRecheckStatus.Success => Ok(ApiResult.Ok(result.Response!)),
            RouteRecheckStatus.InvalidRequest => BadRequest(ApiResult.Fail(result.Message ?? "The route recheck request is invalid.")),
            RouteRecheckStatus.SearchExpired => StatusCode(
                StatusCodes.Status410Gone,
                ApiResult.Fail(result.Message ?? "This route search has expired.")),
            RouteRecheckStatus.Forbidden => StatusCode(
                StatusCodes.Status403Forbidden,
                ApiResult.Fail(result.Message ?? "This route search is not available to this session.")),
            _ => StatusCode(StatusCodes.Status500InternalServerError, ApiResult.Fail("The route recheck could not be completed."))
        };
    }

    private ulong? CurrentUserId() =>
        User.Identity?.IsAuthenticated == true ? User.GetUserId() : null;
}
