using Microsoft.EntityFrameworkCore;
using SafePathBD.Web.Common;
using SafePathBD.Web.Data;
using SafePathBD.Web.Models.DTOs.Moderation;
using SafePathBD.Web.Models.DTOs.Reports;
using SafePathBD.Web.Models.Entities;
using SafePathBD.Web.Services.Interfaces;

namespace SafePathBD.Web.Services.Implementations;

/// <summary>
/// The only place a report's status is allowed to change. Every decision writes
/// verification history plus an audit row, and a verified accident report is promoted
/// into the trusted <c>accidents</c> dataset in the same transaction.
/// </summary>
public sealed class ReportModerationService : IReportModerationService
{
    public const int MaxPageSize = 30;

    private readonly SafePathDbContext _db;
    private readonly IReportService _reportService;
    private readonly ILogger<ReportModerationService> _logger;

    public ReportModerationService(SafePathDbContext db, IReportService reportService, ILogger<ReportModerationService> logger)
    {
        _db = db;
        _reportService = reportService;
        _logger = logger;
    }

    // ------------------------------------------------------------------- queue

    public async Task<PagedResult<ModerationQueueItemDto>> GetQueueAsync(ModerationQueueQuery query, CancellationToken cancellationToken = default)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, MaxPageSize);

        var source = _db.Reports.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.StatusCode))
        {
            source = source.Where(r => r.Status.StatusCode == query.StatusCode);
        }

        if (!string.IsNullOrWhiteSpace(query.ReportType))
        {
            source = source.Where(r => r.ReportType == query.ReportType);
        }

        if (!string.IsNullOrWhiteSpace(query.Severity))
        {
            source = source.Where(r => r.AccidentReports != null && r.AccidentReports.Severity.SeverityName == query.Severity);
        }

        if (!string.IsNullOrWhiteSpace(query.RiskLevel))
        {
            source = source.Where(r => r.HazardReports != null && r.HazardReports.RiskLevel == query.RiskLevel);
        }

        if (query.From is not null)
        {
            source = source.Where(r => r.ReportedAt >= query.From);
        }

        if (query.To is not null)
        {
            var inclusiveEnd = query.To.Value.Date.AddDays(1);
            source = source.Where(r => r.ReportedAt < inclusiveEnd);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            source = source.Where(r =>
                r.Title.Contains(term)
                || (r.Location.AreaName != null && r.Location.AreaName.Contains(term))
                || (r.Location.City != null && r.Location.City.Contains(term))
                || (r.Location.AddressLine != null && r.Location.AddressLine.Contains(term)));
        }

        var total = await source.CountAsync(cancellationToken);

        // Vote counts are computed inside the projection so the queue stays a single round trip.
        var items = await source
            .OrderBy(r => r.Status.IsClosedStatus)
            .ThenBy(r => r.ReportedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
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

        return new PagedResult<ModerationQueueItemDto>(items, page, pageSize, total);
    }

    public async Task<ModerationCountsDto> GetCountsAsync(CancellationToken cancellationToken = default)
    {
        var byStatus = await _db.Reports.AsNoTracking()
            .GroupBy(r => r.Status.StatusCode)
            .Select(g => new { StatusCode = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        int Count(string code) => byStatus.FirstOrDefault(s => s.StatusCode == code)?.Count ?? 0;

        return new ModerationCountsDto(
            Count(ReportStatusCodes.Pending),
            Count(ReportStatusCodes.UnderReview),
            Count(ReportStatusCodes.NeedsInfo),
            Count(ReportStatusCodes.Verified),
            Count(ReportStatusCodes.Rejected),
            Count(ReportStatusCodes.Duplicate),
            Count(ReportStatusCodes.Resolved));
    }

    public async Task<ModerationReportDto?> GetForReviewAsync(ulong reportId, CancellationToken cancellationToken = default)
    {
        // Staff always pass the visibility rule, so this reuses the shared projection.
        var details = await _reportService.GetDetailsAsync(reportId, null, true, cancellationToken);
        if (details is null)
        {
            return null;
        }

        var meta = await _db.Reports.AsNoTracking()
            .Where(r => r.ReportId == reportId)
            .Select(r => new
            {
                r.UserId,
                ReporterName = r.User != null ? r.User.FullName : null,
                ReporterEmail = r.User != null ? r.User.Email : null,
                ConfirmCount = r.ReportVotes.Count(v => v.VoteType == ReportVoteTypes.Confirm),
                DisputeCount = r.ReportVotes.Count(v => v.VoteType == ReportVoteTypes.Dispute),
                CommentCount = r.ReportComments.Count(c => !c.IsDeleted),
                PromotedAccidentId = r.Accidents != null ? (ulong?)r.Accidents.AccidentId : null
            })
            .SingleAsync(cancellationToken);

        var history = await GetHistoryAsync(reportId, cancellationToken);

        return new ModerationReportDto(
            details,
            meta.ReporterName,
            meta.ReporterEmail,
            meta.UserId,
            meta.ConfirmCount,
            meta.DisputeCount,
            meta.CommentCount,
            history,
            ReportStatusTransitions.From(details.StatusCode),
            meta.PromotedAccidentId);
    }

    private Task<List<ReportVerificationEntryDto>> GetHistoryAsync(ulong reportId, CancellationToken cancellationToken) =>
        _db.ReportVerifications.AsNoTracking()
            .Where(v => v.ReportId == reportId)
            .OrderBy(v => v.VerifiedAt)
            .ThenBy(v => v.VerificationId)
            .Select(v => new ReportVerificationEntryDto(
                v.VerificationId,
                v.Status.StatusCode,
                v.Status.StatusName,
                v.AdminUser != null ? v.AdminUser.FullName : null,
                v.AdminComment,
                v.VerifiedAt))
            .ToListAsync(cancellationToken);

    // ---------------------------------------------------------------- decision

    public async Task<ModerationResult> ApplyDecisionAsync(ModerationDecision decision, CancellationToken cancellationToken = default)
    {
        var note = decision.Note?.Trim();

        if (ReportStatusTransitions.RequiresNote(decision.TargetStatusCode) && string.IsNullOrWhiteSpace(note))
        {
            return ModerationResult.Fail(ModerationStatus.NoteRequired, "This decision needs a short explanation.");
        }

        var report = await _db.Reports
            .Include(r => r.Status)
            .Include(r => r.AccidentReports)
            .SingleOrDefaultAsync(r => r.ReportId == decision.ReportId, cancellationToken);

        if (report is null)
        {
            return ModerationResult.Fail(ModerationStatus.NotFound, "That report no longer exists.");
        }

        var currentCode = report.Status.StatusCode;

        // The queue page sends the status it rendered; a mismatch means someone else acted first.
        if (!string.IsNullOrWhiteSpace(decision.ExpectedCurrentStatusCode)
            && !string.Equals(decision.ExpectedCurrentStatusCode, currentCode, StringComparison.Ordinal))
        {
            return ModerationResult.Fail(
                ModerationStatus.StaleState,
                "This report was updated by another reviewer. Refresh to see the latest status.");
        }

        if (!ReportStatusTransitions.IsAllowed(currentCode, decision.TargetStatusCode))
        {
            return ModerationResult.Fail(
                ModerationStatus.InvalidTransition,
                $"A {report.Status.StatusName.ToLowerInvariant()} report cannot be moved to that status.");
        }

        var targetStatus = await _db.ReportStatuses
            .SingleOrDefaultAsync(s => s.StatusCode == decision.TargetStatusCode, cancellationToken);

        if (targetStatus is null)
        {
            return ModerationResult.Fail(
                ModerationStatus.StatusConfigurationMissing, "That status is not configured in the database.");
        }

        var now = DateTime.Now;
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            _db.ReportVerifications.Add(new ReportVerifications
            {
                ReportId = report.ReportId,
                AdminUserId = decision.ReviewerUserId,
                StatusId = targetStatus.StatusId,
                AdminComment = string.IsNullOrWhiteSpace(note) ? null : note,
                VerifiedAt = now
            });

            report.StatusId = targetStatus.StatusId;
            report.UpdatedAt = now;

            if (decision.TargetStatusCode == ReportStatusCodes.Resolved)
            {
                report.ResolvedAt = now;
            }

            _db.AdminActions.Add(new AdminActions
            {
                AdminUserId = decision.ReviewerUserId,
                ActionType = AdminActionTypes.ForStatus(decision.TargetStatusCode),
                EntityType = AdminActionTypes.ReportEntity,
                EntityId = report.ReportId,
                Description = $"{report.ReportType} report \"{report.Title}\" moved from {currentCode} to {decision.TargetStatusCode}.",
                ActionAt = now
            });

            Accidents? promoted = null;
            if (decision.TargetStatusCode == ReportStatusCodes.Verified && report.ReportType == ReportTypes.Accident)
            {
                promoted = await PromoteAccidentAsync(report, decision.ReviewerUserId, now, cancellationToken);
            }

            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            // The generated key is only available once the insert has been flushed.
            return ModerationResult.Ok(targetStatus.StatusCode, targetStatus.StatusName, promoted?.AccidentId);
        }
        catch (DbUpdateException ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "Moderation decision failed for report {ReportId}.", report.ReportId);

            return ModerationResult.Fail(
                ModerationStatus.PromotionFailed,
                "The decision could not be saved. Nothing was changed — please try again.");
        }
    }

    /// <summary>
    /// Copies a verified community accident claim into the trusted dataset.
    /// Returns null when this report was already promoted; the unique key on
    /// <c>accidents.source_report_id</c> is the real guarantee.
    /// </summary>
    private async Task<Accidents?> PromoteAccidentAsync(Reports report, ulong reviewerUserId, DateTime now, CancellationToken cancellationToken)
    {
        var alreadyPromoted = await _db.Accidents
            .AnyAsync(a => a.SourceReportId == report.ReportId, cancellationToken);

        if (alreadyPromoted || report.AccidentReports is null)
        {
            return null;
        }

        var claim = report.AccidentReports;

        var accident = new Accidents
        {
            SourceReportId = report.ReportId,
            LocationId = report.LocationId,
            RoadSegmentId = report.RoadSegmentId,
            AccidentTypeId = claim.AccidentTypeId,
            SeverityId = claim.SeverityId,
            // accidents.accident_occurred_at is NOT NULL; fall back to when the report was filed.
            AccidentOccurredAt = claim.AccidentOccurredAt ?? report.ReportedAt,
            NumberOfVehicles = claim.NumberOfVehicles,
            NumberOfInjured = claim.NumberOfInjured,
            NumberOfDeaths = claim.NumberOfDeaths,
            WeatherCondition = claim.WeatherNotes,
            Description = report.Description,
            VerifiedBy = reviewerUserId,
            VerifiedAt = now,
            CreatedAt = now,
            UpdatedAt = now
        };

        _db.Accidents.Add(accident);

        _db.AdminActions.Add(new AdminActions
        {
            AdminUserId = reviewerUserId,
            ActionType = AdminActionTypes.AccidentPromoted,
            EntityType = AdminActionTypes.AccidentEntity,
            EntityId = report.ReportId,
            Description = $"Verified accident report {report.ReportId} promoted to the trusted accident dataset.",
            ActionAt = now
        });

        return accident;
    }

    public async Task<IReadOnlyList<AdminActionEntryDto>> GetRecentActionsAsync(int take, CancellationToken cancellationToken = default) =>
        await _db.AdminActions.AsNoTracking()
            .OrderByDescending(a => a.ActionAt)
            .ThenByDescending(a => a.AdminActionId)
            .Take(Math.Clamp(take, 1, 50))
            .Select(a => new AdminActionEntryDto(
                a.AdminActionId,
                a.ActionType,
                a.EntityType,
                a.EntityId,
                a.Description,
                a.AdminUser != null ? a.AdminUser.FullName : null,
                a.ActionAt))
            .ToListAsync(cancellationToken);
}
