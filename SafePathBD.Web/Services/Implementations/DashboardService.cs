using Microsoft.EntityFrameworkCore;
using SafePathBD.Web.Common;
using SafePathBD.Web.Data;
using SafePathBD.Web.Models.DTOs.Dashboard;
using SafePathBD.Web.Models.DTOs.Moderation;
using SafePathBD.Web.Models.DTOs.Reports;
using SafePathBD.Web.Models.Entities;
using SafePathBD.Web.Services.Interfaces;

namespace SafePathBD.Web.Services.Implementations;

/// <summary>
/// Read-only operational projections for user, moderator and admin dashboards.
/// Queries stay aggregate/projection based; no dashboard loads full entity graphs.
/// </summary>
public sealed class DashboardService : IDashboardService
{
    private readonly SafePathDbContext _db;
    private readonly INotificationService _notifications;
    private readonly IReportModerationService _moderation;

    public DashboardService(
        SafePathDbContext db,
        INotificationService notifications,
        IReportModerationService moderation)
    {
        _db = db;
        _notifications = notifications;
        _moderation = moderation;
    }

    public async Task<UserDashboardDto> GetUserDashboardAsync(
        ulong userId,
        CancellationToken cancellationToken = default)
    {
        var counts = await _db.Reports.AsNoTracking()
            .Where(r => r.UserId == userId)
            .GroupBy(r => r.Status.StatusCode)
            .Select(g => new { StatusCode = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        int Count(string status) => counts.FirstOrDefault(x => x.StatusCode == status)?.Count ?? 0;

        var recentReports = await _db.Reports.AsNoTracking()
            .Where(r => r.UserId == userId)
            .OrderByDescending(r => r.ReportedAt)
            .Take(5)
            .Select(ReportSummaryProjection)
            .ToListAsync(cancellationToken);

        var actionRequired = await _db.Reports.AsNoTracking()
            .Where(r => r.UserId == userId && r.Status.StatusCode == ReportStatusCodes.NeedsInfo)
            .OrderByDescending(r => r.UpdatedAt)
            .Take(5)
            .Select(r => new NeedsInfoReportDto(
                r.ReportId,
                r.Title,
                r.UpdatedAt,
                r.ReportVerifications
                    .Where(v => v.Status.StatusCode == ReportStatusCodes.NeedsInfo)
                    .OrderByDescending(v => v.VerifiedAt)
                    .ThenByDescending(v => v.VerificationId)
                    .Select(v => v.AdminComment)
                    .FirstOrDefault()))
            .ToListAsync(cancellationToken);

        var recentNotifications = await _notifications.GetLatestAsync(userId, 5, cancellationToken);
        var unreadNotifications = await _notifications.GetUnreadCountAsync(userId, cancellationToken);

        // Activity is intentionally composed from existing authoritative records; no fake activity table.
        var activity = recentReports
            .Select(r => new UserActivityItemDto(
                "REPORT_SUBMITTED",
                "Report submitted",
                r.Title,
                r.ReportedAt,
                r.ReportId,
                r.StatusCode))
            .Concat(recentNotifications.Select(n => new UserActivityItemDto(
                "NOTIFICATION",
                n.Title,
                n.Message,
                n.CreatedAt,
                n.ReportId,
                null)))
            .OrderByDescending(x => x.OccurredAt)
            .Take(6)
            .ToList();

        return new UserDashboardDto(
            counts.Sum(x => x.Count),
            Count(ReportStatusCodes.Pending),
            Count(ReportStatusCodes.UnderReview),
            Count(ReportStatusCodes.NeedsInfo),
            Count(ReportStatusCodes.Verified),
            Count(ReportStatusCodes.Rejected),
            Count(ReportStatusCodes.Duplicate),
            Count(ReportStatusCodes.Resolved),
            unreadNotifications,
            recentReports,
            actionRequired,
            recentNotifications,
            activity);
    }

    public async Task<ModeratorDashboardDto> GetModeratorDashboardAsync(CancellationToken cancellationToken = default)
    {
        var counts = await _moderation.GetCountsAsync(cancellationToken);
        var queuePreview = await GetOpenQueuePreviewAsync(6, cancellationToken);

        var recent = await _db.ReportVerifications.AsNoTracking()
            .OrderByDescending(v => v.VerifiedAt)
            .ThenByDescending(v => v.VerificationId)
            .Take(8)
            .Select(v => new ModeratorActivityDto(
                v.VerificationId,
                v.ReportId,
                v.Report.Title,
                v.Status.StatusCode,
                v.Status.StatusName,
                v.AdminUser != null ? v.AdminUser.FullName : null,
                v.VerifiedAt))
            .ToListAsync(cancellationToken);

        return new ModeratorDashboardDto(counts, queuePreview, recent);
    }

    public async Task<AdminDashboardDto> GetAdminDashboardAsync(CancellationToken cancellationToken = default)
    {
        var totalUsers = await _db.Users.AsNoTracking().CountAsync(cancellationToken);
        var activeUsers = await _db.Users.AsNoTracking().CountAsync(u => u.IsActive == true, cancellationToken);

        var reportTypes = await _db.Reports.AsNoTracking()
            .GroupBy(r => r.ReportType)
            .Select(g => new { Type = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        int TypeCount(string type) => reportTypes.FirstOrDefault(x => x.Type == type)?.Count ?? 0;

        var counts = await _moderation.GetCountsAsync(cancellationToken);
        var recentActions = await _moderation.GetRecentActionsAsync(10, cancellationToken);
        var queuePreview = await GetOpenQueuePreviewAsync(5, cancellationToken);

        return new AdminDashboardDto(
            totalUsers,
            activeUsers,
            TypeCount(ReportTypes.Accident),
            TypeCount(ReportTypes.Hazard),
            counts,
            recentActions,
            queuePreview);
    }

    private async Task<IReadOnlyList<ModerationQueueItemDto>> GetOpenQueuePreviewAsync(
        int take,
        CancellationToken cancellationToken)
    {
        var openStatuses = new[]
        {
            ReportStatusCodes.Pending,
            ReportStatusCodes.UnderReview,
            ReportStatusCodes.NeedsInfo
        };

        return await _db.Reports.AsNoTracking()
            .Where(r => openStatuses.Contains(r.Status.StatusCode))
            .OrderBy(r => r.Status.StatusCode == ReportStatusCodes.NeedsInfo ? 0
                : r.Status.StatusCode == ReportStatusCodes.UnderReview ? 1 : 2)
            .ThenBy(r => r.ReportedAt)
            .Take(Math.Clamp(take, 1, 10))
            .Select(r => new ModerationQueueItemDto(
                r.ReportId,
                r.ReportType,
                r.Title,
                r.Status.StatusCode,
                r.Status.StatusName,
                r.ReportedAt,
                r.Location.AreaName,
                r.Location.City,
                r.AccidentReports != null ? r.AccidentReports.Severity.SeverityName : null,
                r.AccidentReports != null ? r.AccidentReports.AccidentType.TypeName : null,
                r.HazardReports != null ? r.HazardReports.HazardType.HazardName : null,
                r.HazardReports != null ? r.HazardReports.RiskLevel : null,
                r.ReportImages.Count,
                r.ReportVotes.Count(v => v.VoteType == ReportVoteTypes.Confirm),
                r.ReportVotes.Count(v => v.VoteType == ReportVoteTypes.Dispute),
                r.IsPublic == true))
            .ToListAsync(cancellationToken);
    }

    private static System.Linq.Expressions.Expression<Func<Reports, ReportSummaryDto>> ReportSummaryProjection =>
        r => new ReportSummaryDto(
            r.ReportId,
            r.ReportType,
            r.Title,
            r.Status.StatusCode,
            r.Status.StatusName,
            r.ReportedAt,
            r.IsPublic == true,
            (double)r.Location.Latitude,
            (double)r.Location.Longitude,
            r.Location.AreaName,
            r.Location.City,
            r.AccidentReports != null ? r.AccidentReports.Severity.SeverityName : null,
            r.AccidentReports != null ? r.AccidentReports.AccidentType.TypeName : null,
            r.HazardReports != null ? r.HazardReports.HazardType.HazardName : null,
            r.HazardReports != null ? r.HazardReports.RiskLevel : null,
            r.ReportImages.OrderBy(i => i.UploadedAt).Select(i => (ulong?)i.ImageId).FirstOrDefault(),
            r.ReportImages.Count);
}
