using SafePathBD.Web.Common;
using SafePathBD.Web.Models.DTOs.Reports;

namespace SafePathBD.Web.Services.Interfaces;

public enum CommunityStatus
{
    Success,
    ReportNotFound,
    NotVisible,
    OwnReport,
    InvalidVoteType,
    EmptyComment,
    CommentTooLong,
    ParentNotFound
}

public sealed record CommunityResult<T>(CommunityStatus Status, string? Message, T? Data)
{
    public bool Succeeded => Status == CommunityStatus.Success;

    public static CommunityResult<T> Ok(T data) => new(CommunityStatus.Success, null, data);

    public static CommunityResult<T> Fail(CommunityStatus status, string message) => new(status, message, default);
}

/// <summary>Who is asking. Resolved from the cookie on the server, never from the request body.</summary>
public sealed record CommunityViewer(ulong? UserId, bool IsStaff)
{
    public bool IsAuthenticated => UserId is > 0;
}

public interface IReportCommunityService
{
    /// <summary>Aggregate confirm/dispute counts plus the caller's own vote.</summary>
    Task<ReportVoteSummaryDto?> GetVoteSummaryAsync(ulong reportId, CommunityViewer viewer, CancellationToken cancellationToken = default);

    /// <summary>Creates or switches the caller's single vote. Re-sending the same vote clears it.</summary>
    Task<CommunityResult<ReportVoteSummaryDto>> CastVoteAsync(ulong reportId, ulong userId, string voteType, CancellationToken cancellationToken = default);

    Task<CommunityResult<ReportVoteSummaryDto>> RemoveVoteAsync(ulong reportId, ulong userId, CancellationToken cancellationToken = default);

    /// <summary>Top-level comments with their replies, newest page first.</summary>
    Task<PagedResult<ReportCommentDto>> GetCommentsAsync(ulong reportId, CommunityViewer viewer, int page, int pageSize, CancellationToken cancellationToken = default);

    Task<int> GetCommentCountAsync(ulong reportId, CancellationToken cancellationToken = default);

    Task<CommunityResult<ReportCommentDto>> AddCommentAsync(ulong reportId, ulong userId, string text, ulong? parentCommentId, CancellationToken cancellationToken = default);

    /// <summary>Soft-deletes a comment. Allowed for the comment author, a moderator or an administrator.</summary>
    Task<CommunityResult<bool>> DeleteCommentAsync(ulong commentId, CommunityViewer viewer, CancellationToken cancellationToken = default);
}
