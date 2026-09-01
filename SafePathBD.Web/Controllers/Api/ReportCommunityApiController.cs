using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SafePathBD.Web.Common;
using SafePathBD.Web.Security;
using SafePathBD.Web.Services.Implementations;
using SafePathBD.Web.Services.Interfaces;

namespace SafePathBD.Web.Controllers.Api;

/// <summary>
/// Community trust signals and discussion for a single report.
/// Every write derives the acting user from the auth cookie, never from the payload.
/// </summary>
[ApiController]
[Route("api/v1/reports/{reportId:long}")]
public class ReportCommunityApiController : ControllerBase
{
    private readonly IReportCommunityService _community;

    public ReportCommunityApiController(IReportCommunityService community)
    {
        _community = community;
    }

    public sealed record VoteRequest(string? VoteType);

    public sealed record CommentRequest(string? Text, ulong? ParentCommentId);

    private CommunityViewer Viewer => new(
        User.Identity?.IsAuthenticated == true ? User.GetUserId() : null,
        User.IsInRole(RoleNames.Admin) || User.IsInRole(RoleNames.Moderator));

    // ------------------------------------------------------------------- votes

    [HttpGet("votes")]
    public async Task<IActionResult> Votes(long reportId, CancellationToken cancellationToken)
    {
        var summary = await _community.GetVoteSummaryAsync((ulong)reportId, Viewer, cancellationToken);
        return summary is null
            ? NotFound(ApiResult.Fail("That report is not available."))
            : Ok(ApiResult.Ok(summary));
    }

    [HttpPost("vote")]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Vote(long reportId, [FromBody] VoteRequest request, CancellationToken cancellationToken)
    {
        var result = await _community.CastVoteAsync((ulong)reportId, User.GetUserId(), request?.VoteType ?? string.Empty, cancellationToken);
        return result.Succeeded ? Ok(ApiResult.Ok(result.Data!)) : Problem(result.Status, result.Message);
    }

    [HttpDelete("vote")]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveVote(long reportId, CancellationToken cancellationToken)
    {
        var result = await _community.RemoveVoteAsync((ulong)reportId, User.GetUserId(), cancellationToken);
        return result.Succeeded ? Ok(ApiResult.Ok(result.Data!)) : Problem(result.Status, result.Message);
    }

    // ---------------------------------------------------------------- comments

    [HttpGet("comments")]
    public async Task<IActionResult> Comments(
        long reportId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = ReportCommunityService.DefaultCommentPageSize,
        CancellationToken cancellationToken = default)
    {
        var comments = await _community.GetCommentsAsync((ulong)reportId, Viewer, page, pageSize, cancellationToken);

        return Ok(ApiResult.Ok(new
        {
            items = comments.Items,
            page = comments.Page,
            pageSize = comments.PageSize,
            totalCount = comments.TotalCount,
            hasNext = comments.HasNext
        }));
    }

    [HttpPost("comments")]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddComment(long reportId, [FromBody] CommentRequest request, CancellationToken cancellationToken)
    {
        var result = await _community.AddCommentAsync(
            (ulong)reportId, User.GetUserId(), request?.Text ?? string.Empty, request?.ParentCommentId, cancellationToken);

        return result.Succeeded ? Ok(ApiResult.Ok(result.Data!)) : Problem(result.Status, result.Message);
    }

    [HttpDelete("comments/{commentId:long}")]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteComment(long reportId, long commentId, CancellationToken cancellationToken)
    {
        var result = await _community.DeleteCommentAsync((ulong)commentId, Viewer, cancellationToken);
        return result.Succeeded ? Ok(ApiResult.Ok(new { commentId })) : Problem(result.Status, result.Message);
    }

    /// <summary>Maps a service outcome to an HTTP status without leaking internals.</summary>
    private IActionResult Problem(CommunityStatus status, string? message)
    {
        var safeMessage = message ?? "The request could not be completed.";

        return status switch
        {
            CommunityStatus.ReportNotFound or CommunityStatus.NotVisible or CommunityStatus.ParentNotFound
                => NotFound(ApiResult.Fail(safeMessage)),
            CommunityStatus.OwnReport => StatusCode(StatusCodes.Status403Forbidden, ApiResult.Fail(safeMessage)),
            _ => BadRequest(ApiResult.Fail(safeMessage))
        };
    }
}
