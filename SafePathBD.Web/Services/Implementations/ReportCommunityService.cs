using Microsoft.EntityFrameworkCore;
using SafePathBD.Web.Common;
using SafePathBD.Web.Data;
using SafePathBD.Web.Models.DTOs.Reports;
using SafePathBD.Web.Models.Entities;
using SafePathBD.Web.Services.Interfaces;

namespace SafePathBD.Web.Services.Implementations;

/// <summary>
/// Community confirm/dispute votes and report discussion.
/// Votes are decision support for moderators; they never change a report's status.
/// </summary>
public sealed class ReportCommunityService : IReportCommunityService
{
    public const int MaxCommentLength = 1500;
    public const int MaxCommentPageSize = 30;
    public const int DefaultCommentPageSize = 10;

    private readonly SafePathDbContext _db;

    public ReportCommunityService(SafePathDbContext db)
    {
        _db = db;
    }

    // ------------------------------------------------------------------- votes

    public async Task<ReportVoteSummaryDto?> GetVoteSummaryAsync(ulong reportId, CommunityViewer viewer, CancellationToken cancellationToken = default)
    {
        var report = await LoadVisibleReportAsync(reportId, viewer, cancellationToken);
        return report is null ? null : await BuildSummaryAsync(report, viewer.UserId, cancellationToken);
    }

    public async Task<CommunityResult<ReportVoteSummaryDto>> CastVoteAsync(ulong reportId, ulong userId, string voteType, CancellationToken cancellationToken = default)
    {
        var normalized = ReportVoteTypes.Normalize(voteType);
        if (normalized is null)
        {
            return CommunityResult<ReportVoteSummaryDto>.Fail(
                CommunityStatus.InvalidVoteType, "A vote must be either a confirmation or a dispute.");
        }

        var viewer = new CommunityViewer(userId, false);
        var report = await LoadVisibleReportAsync(reportId, viewer, cancellationToken);
        if (report is null)
        {
            return CommunityResult<ReportVoteSummaryDto>.Fail(
                CommunityStatus.ReportNotFound, "That report is not available.");
        }

        if (report.UserId == userId)
        {
            return CommunityResult<ReportVoteSummaryDto>.Fail(
                CommunityStatus.OwnReport, "You cannot vote on your own report.");
        }

        var existing = await _db.ReportVotes
            .SingleOrDefaultAsync(v => v.ReportId == reportId && v.UserId == userId, cancellationToken);

        if (existing is null)
        {
            _db.ReportVotes.Add(new ReportVotes
            {
                ReportId = reportId,
                UserId = userId,
                VoteType = normalized,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            });
        }
        else if (existing.VoteType == normalized)
        {
            // Pressing the active choice again withdraws the vote.
            _db.ReportVotes.Remove(existing);
        }
        else
        {
            existing.VoteType = normalized;
            existing.UpdatedAt = DateTime.Now;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return CommunityResult<ReportVoteSummaryDto>.Ok(await BuildSummaryAsync(report, userId, cancellationToken));
    }

    public async Task<CommunityResult<ReportVoteSummaryDto>> RemoveVoteAsync(ulong reportId, ulong userId, CancellationToken cancellationToken = default)
    {
        var viewer = new CommunityViewer(userId, false);
        var report = await LoadVisibleReportAsync(reportId, viewer, cancellationToken);
        if (report is null)
        {
            return CommunityResult<ReportVoteSummaryDto>.Fail(
                CommunityStatus.ReportNotFound, "That report is not available.");
        }

        var existing = await _db.ReportVotes
            .SingleOrDefaultAsync(v => v.ReportId == reportId && v.UserId == userId, cancellationToken);

        if (existing is not null)
        {
            _db.ReportVotes.Remove(existing);
            await _db.SaveChangesAsync(cancellationToken);
        }

        return CommunityResult<ReportVoteSummaryDto>.Ok(await BuildSummaryAsync(report, userId, cancellationToken));
    }

    private async Task<ReportVoteSummaryDto> BuildSummaryAsync(Reports report, ulong? userId, CancellationToken cancellationToken)
    {
        var tally = await _db.ReportVotes.AsNoTracking()
            .Where(v => v.ReportId == report.ReportId)
            .GroupBy(v => v.VoteType)
            .Select(g => new { VoteType = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var confirm = tally.FirstOrDefault(t => t.VoteType == ReportVoteTypes.Confirm)?.Count ?? 0;
        var dispute = tally.FirstOrDefault(t => t.VoteType == ReportVoteTypes.Dispute)?.Count ?? 0;

        string? mine = null;
        if (userId is > 0)
        {
            mine = await _db.ReportVotes.AsNoTracking()
                .Where(v => v.ReportId == report.ReportId && v.UserId == userId)
                .Select(v => v.VoteType)
                .SingleOrDefaultAsync(cancellationToken);
        }

        var (canVote, reason) = ResolveVoteEligibility(report, userId);
        return new ReportVoteSummaryDto(report.ReportId, confirm, dispute, mine, canVote, reason);
    }

    private static (bool CanVote, string? Reason) ResolveVoteEligibility(Reports report, ulong? userId)
    {
        if (userId is not > 0)
        {
            return (false, "Sign in to add your confirmation.");
        }

        if (report.UserId == userId)
        {
            return (false, "Community feedback is available from other users.");
        }

        return (true, null);
    }

    // ---------------------------------------------------------------- comments

    public async Task<PagedResult<ReportCommentDto>> GetCommentsAsync(
        ulong reportId, CommunityViewer viewer, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var safePage = Math.Max(1, page);
        var safeSize = Math.Clamp(pageSize, 1, MaxCommentPageSize);

        var report = await LoadVisibleReportAsync(reportId, viewer, cancellationToken);
        if (report is null)
        {
            return new PagedResult<ReportCommentDto>(Array.Empty<ReportCommentDto>(), safePage, safeSize, 0);
        }

        var roots = _db.ReportComments.AsNoTracking()
            .Where(c => c.ReportId == reportId && c.ParentCommentId == null);

        var total = await roots.CountAsync(cancellationToken);

        var rootRows = await roots
            .OrderByDescending(c => c.CreatedAt)
            .Skip((safePage - 1) * safeSize)
            .Take(safeSize)
            .Select(c => new CommentRow(
                c.CommentId,
                c.ParentCommentId,
                c.UserId,
                c.User!.FullName,
                c.CommentText,
                c.IsDeleted,
                c.CreatedAt,
                c.User!.UserRoles.Any(ur => ur.Role.RoleName == RoleNames.Admin || ur.Role.RoleName == RoleNames.Moderator)))
            .ToListAsync(cancellationToken);

        var rootIds = rootRows.Select(r => r.CommentId).ToList();

        // One extra query for every reply on this page, instead of one query per comment.
        var replyRows = await _db.ReportComments.AsNoTracking()
            .Where(c => c.ParentCommentId != null && rootIds.Contains(c.ParentCommentId!.Value))
            .OrderBy(c => c.CreatedAt)
            .Select(c => new CommentRow(
                c.CommentId,
                c.ParentCommentId,
                c.UserId,
                c.User!.FullName,
                c.CommentText,
                c.IsDeleted,
                c.CreatedAt,
                c.User!.UserRoles.Any(ur => ur.Role.RoleName == RoleNames.Admin || ur.Role.RoleName == RoleNames.Moderator)))
            .ToListAsync(cancellationToken);

        var repliesByParent = replyRows
            .GroupBy(r => r.ParentCommentId!.Value)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<CommentRow>)g.ToList());

        var items = rootRows
            .Select(root => Project(
                root,
                viewer,
                repliesByParent.TryGetValue(root.CommentId, out var kids)
                    ? kids.Select(k => Project(k, viewer, Array.Empty<ReportCommentDto>())).ToList()
                    : Array.Empty<ReportCommentDto>()))
            .ToList();

        return new PagedResult<ReportCommentDto>(items, safePage, safeSize, total);
    }

    public Task<int> GetCommentCountAsync(ulong reportId, CancellationToken cancellationToken = default) =>
        _db.ReportComments.AsNoTracking().CountAsync(c => c.ReportId == reportId && !c.IsDeleted, cancellationToken);

    public async Task<CommunityResult<ReportCommentDto>> AddCommentAsync(
        ulong reportId, ulong userId, string text, ulong? parentCommentId, CancellationToken cancellationToken = default)
    {
        var trimmed = (text ?? string.Empty).Trim();
        if (trimmed.Length == 0)
        {
            return CommunityResult<ReportCommentDto>.Fail(CommunityStatus.EmptyComment, "Write something before posting.");
        }

        if (trimmed.Length > MaxCommentLength)
        {
            return CommunityResult<ReportCommentDto>.Fail(
                CommunityStatus.CommentTooLong, $"Comments are limited to {MaxCommentLength} characters.");
        }

        var viewer = new CommunityViewer(userId, false);
        var report = await LoadVisibleReportAsync(reportId, viewer, cancellationToken);
        if (report is null)
        {
            return CommunityResult<ReportCommentDto>.Fail(CommunityStatus.ReportNotFound, "That report is not available.");
        }

        // Replies may only attach to a top-level comment on this same report, so nesting stays one level deep.
        if (parentCommentId is not null)
        {
            var parentIsValid = await _db.ReportComments.AsNoTracking()
                .AnyAsync(c => c.CommentId == parentCommentId && c.ReportId == reportId && c.ParentCommentId == null, cancellationToken);

            if (!parentIsValid)
            {
                return CommunityResult<ReportCommentDto>.Fail(
                    CommunityStatus.ParentNotFound, "The comment you replied to is no longer available.");
            }
        }

        var entity = new ReportComments
        {
            ReportId = reportId,
            UserId = userId,
            ParentCommentId = parentCommentId,
            CommentText = trimmed,
            IsDeleted = false,
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now
        };

        _db.ReportComments.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);

        var author = await _db.Users.AsNoTracking()
            .Where(u => u.UserId == userId)
            .Select(u => new
            {
                u.FullName,
                IsStaff = u.UserRoles.Any(ur => ur.Role.RoleName == RoleNames.Admin || ur.Role.RoleName == RoleNames.Moderator)
            })
            .SingleOrDefaultAsync(cancellationToken);

        var row = new CommentRow(
            entity.CommentId,
            entity.ParentCommentId,
            userId,
            author?.FullName,
            entity.CommentText,
            false,
            entity.CreatedAt,
            author?.IsStaff ?? false);

        return CommunityResult<ReportCommentDto>.Ok(Project(row, viewer, Array.Empty<ReportCommentDto>()));
    }

    public async Task<CommunityResult<bool>> DeleteCommentAsync(ulong commentId, CommunityViewer viewer, CancellationToken cancellationToken = default)
    {
        var comment = await _db.ReportComments.SingleOrDefaultAsync(c => c.CommentId == commentId, cancellationToken);
        if (comment is null || comment.IsDeleted)
        {
            return CommunityResult<bool>.Fail(CommunityStatus.ReportNotFound, "That comment is no longer available.");
        }

        var isAuthor = viewer.UserId is > 0 && comment.UserId == viewer.UserId;
        if (!isAuthor && !viewer.IsStaff)
        {
            return CommunityResult<bool>.Fail(CommunityStatus.NotVisible, "You cannot remove that comment.");
        }

        // Soft delete keeps the thread shape intact without exposing the removed text.
        comment.IsDeleted = true;
        comment.UpdatedAt = DateTime.Now;
        await _db.SaveChangesAsync(cancellationToken);

        return CommunityResult<bool>.Ok(true);
    }

    // ------------------------------------------------------------------ shared

    private sealed record CommentRow(
        ulong CommentId,
        ulong? ParentCommentId,
        ulong? UserId,
        string? AuthorName,
        string Text,
        bool IsDeleted,
        DateTime CreatedAt,
        bool AuthorIsStaff);

    private static ReportCommentDto Project(CommentRow row, CommunityViewer viewer, IReadOnlyList<ReportCommentDto> replies)
    {
        var name = row.AuthorName ?? "Removed account";
        var isOwn = viewer.UserId is > 0 && row.UserId == viewer.UserId;

        return new ReportCommentDto(
            row.CommentId,
            row.ParentCommentId,
            row.IsDeleted ? "Removed comment" : name,
            row.IsDeleted ? "—" : Initials(name),
            !row.IsDeleted && row.AuthorIsStaff,
            isOwn,
            row.IsDeleted,
            row.IsDeleted ? "This comment was removed." : row.Text,
            row.CreatedAt,
            replies);
    }

    private static string Initials(string name)
    {
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            return "?";
        }

        return parts.Length == 1
            ? parts[0][..1].ToUpperInvariant()
            : (parts[0][..1] + parts[^1][..1]).ToUpperInvariant();
    }

    /// <summary>
    /// Applies exactly the same visibility rule as report details: owner, staff,
    /// or a report that is both public and verified.
    /// </summary>
    private async Task<Reports?> LoadVisibleReportAsync(ulong reportId, CommunityViewer viewer, CancellationToken cancellationToken)
    {
        var report = await _db.Reports.AsNoTracking()
            .Include(r => r.Status)
            .SingleOrDefaultAsync(r => r.ReportId == reportId, cancellationToken);

        if (report is null)
        {
            return null;
        }

        var isOwner = viewer.UserId is > 0 && report.UserId == viewer.UserId;
        var isPubliclyVisible = report.IsPublic == true && report.Status.StatusCode == ReportStatusCodes.Verified;

        return isOwner || viewer.IsStaff || isPubliclyVisible ? report : null;
    }
}
