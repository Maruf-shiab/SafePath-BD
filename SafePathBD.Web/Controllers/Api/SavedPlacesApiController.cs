using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SafePathBD.Web.Common;
using SafePathBD.Web.Models.DTOs.SavedPlaces;
using SafePathBD.Web.Security;
using SafePathBD.Web.Services.Interfaces;

namespace SafePathBD.Web.Controllers.Api;

[ApiController]
[Authorize]
[Route("api/v1/saved-places")]
public sealed class SavedPlacesApiController : ControllerBase
{
    private readonly ISavedPlaceService _savedPlaces;

    public SavedPlacesApiController(ISavedPlaceService savedPlaces)
    {
        _savedPlaces = savedPlaces;
    }

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var items = await _savedPlaces.GetForUserAsync(User.GetUserId(), cancellationToken);
        return Ok(ApiResult.Ok(items));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(8 * 1024)]
    public async Task<IActionResult> Save(
        [FromBody] SavePlaceRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _savedPlaces.SaveAsync(User.GetUserId(), request, cancellationToken);
        return result.Status switch
        {
            SavePlaceStatus.Success => Ok(ApiResult.Ok(result.Place!, result.Message)),
            SavePlaceStatus.Invalid => BadRequest(ApiResult.Fail(result.Message ?? "The saved place is invalid.")),
            SavePlaceStatus.DuplicateName => Conflict(ApiResult.Fail(result.Message ?? "That saved-place name already exists.")),
            _ => StatusCode(StatusCodes.Status500InternalServerError, ApiResult.Fail("The location could not be saved."))
        };
    }

    [HttpDelete("{savedPlaceId:long}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(ulong savedPlaceId, CancellationToken cancellationToken)
    {
        var deleted = await _savedPlaces.DeleteAsync(User.GetUserId(), savedPlaceId, cancellationToken);
        return deleted
            ? Ok(ApiResult.Ok(new { savedPlaceId }, "Saved place removed."))
            : NotFound(ApiResult.Fail("Saved place not found."));
    }
}
