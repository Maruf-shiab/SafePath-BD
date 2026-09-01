using Microsoft.EntityFrameworkCore;
using SafePathBD.Web.Common;
using SafePathBD.Web.Data;
using SafePathBD.Web.Models.DTOs.Reports;
using SafePathBD.Web.Models.Entities;
using SafePathBD.Web.Services.Interfaces;

namespace SafePathBD.Web.Services.Implementations;

public sealed class ReportService : IReportService
{
    public const int MaxPageSize = 50;
    public const int MaxMapReports = 300;

    private readonly SafePathDbContext _db;

    public ReportService(SafePathDbContext db)
    {
        _db = db;
    }

    public async Task<ReportLookupsDto> GetLookupsAsync(CancellationToken cancellationToken = default)
    {
        var accidentTypes = await _db.AccidentTypes.AsNoTracking()
            .Where(t => t.IsActive == true)
            .OrderBy(t => t.TypeName)
            .Select(t => new ReportLookupItem(t.AccidentTypeId, t.TypeName, t.Description))
            .ToListAsync(cancellationToken);

        var severities = await _db.AccidentSeverities.AsNoTracking()
            .OrderBy(s => s.RiskWeight)
            .Select(s => new ReportLookupItem(s.SeverityId, s.SeverityName, s.Description))
            .ToListAsync(cancellationToken);

        var hazardTypes = await _db.HazardTypes.AsNoTracking()
            .Where(t => t.IsActive == true)
            .OrderBy(t => t.HazardName)
            .Select(t => new ReportLookupItem(t.HazardTypeId, t.HazardName, t.Description))
            .ToListAsync(cancellationToken);

        return new ReportLookupsDto(accidentTypes, severities, hazardTypes);
    }

    public async Task<PagedResult<ReportSummaryDto>> GetMyReportsAsync(MyReportsQuery query, CancellationToken cancellationToken = default)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, MaxPageSize);

        // Ownership always comes from the authenticated id supplied by the caller.
        var source = _db.Reports.AsNoTracking().Where(r => r.UserId == query.UserId);

        if (!string.IsNullOrWhiteSpace(query.ReportType))
        {
            source = source.Where(r => r.ReportType == query.ReportType);
        }

        if (!string.IsNullOrWhiteSpace(query.StatusCode))
        {
            source = source.Where(r => r.Status.StatusCode == query.StatusCode);
        }

        var totalCount = await source.CountAsync(cancellationToken);

        var items = await source
            .OrderByDescending(r => r.ReportedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(SummaryProjection)
            .ToListAsync(cancellationToken);

        return new PagedResult<ReportSummaryDto>(items, page, pageSize, totalCount);
    }

    public async Task<MyReportStatsDto> GetMyReportStatsAsync(ulong userId, CancellationToken cancellationToken = default)
    {
        var counts = await _db.Reports.AsNoTracking()
            .Where(r => r.UserId == userId)
            .GroupBy(r => r.Status.StatusCode)
            .Select(g => new { Code = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        int For(string code) => counts.FirstOrDefault(c => c.Code == code)?.Count ?? 0;

        return new MyReportStatsDto(
            counts.Sum(c => c.Count),
            For(ReportStatusCodes.Pending) + For(ReportStatusCodes.UnderReview) + For(ReportStatusCodes.NeedsInfo),
            For(ReportStatusCodes.Verified),
            For(ReportStatusCodes.Resolved),
            For(ReportStatusCodes.Rejected));
    }

    public async Task<IReadOnlyList<ReportSummaryDto>> GetRecentReportsForUserAsync(ulong userId, int take, CancellationToken cancellationToken = default)
    {
        return await _db.Reports.AsNoTracking()
            .Where(r => r.UserId == userId)
            .OrderByDescending(r => r.ReportedAt)
            .Take(Math.Clamp(take, 1, 10))
            .Select(SummaryProjection)
            .ToListAsync(cancellationToken);
    }

    public async Task<ReportDetailsDto?> GetDetailsAsync(
        ulong reportId,
        ulong? viewerUserId,
        bool viewerIsStaff,
        CancellationToken cancellationToken = default)
    {
        var report = await _db.Reports.AsNoTracking()
            .Where(r => r.ReportId == reportId)
            .Select(r => new
            {
                r.ReportId,
                r.ReportType,
                r.Title,
                r.Description,
                r.UserId,
                r.IsPublic,
                r.ReportedAt,
                r.ResolvedAt,
                r.Status.StatusCode,
                r.Status.StatusName,
                StatusDescription = r.Status.Description,
                r.Status.IsClosedStatus,
                ReporterName = r.User != null ? r.User.FullName : null,
                r.Location.Latitude,
                r.Location.Longitude,
                r.Location.AddressLine,
                r.Location.LandmarkName,
                r.Location.AreaName,
                r.Location.City,
                r.Location.District,
                AccidentTypeName = r.AccidentReports != null ? r.AccidentReports.AccidentType.TypeName : null,
                SeverityName = r.AccidentReports != null ? r.AccidentReports.Severity.SeverityName : null,
                AccidentOccurredAt = r.AccidentReports != null ? r.AccidentReports.AccidentOccurredAt : null,
                NumberOfVehicles = r.AccidentReports != null ? r.AccidentReports.NumberOfVehicles : null,
                NumberOfInjured = r.AccidentReports != null ? (ushort?)r.AccidentReports.NumberOfInjured : null,
                NumberOfDeaths = r.AccidentReports != null ? (ushort?)r.AccidentReports.NumberOfDeaths : null,
                WeatherNotes = r.AccidentReports != null ? r.AccidentReports.WeatherNotes : null,
                HazardTypeName = r.HazardReports != null ? r.HazardReports.HazardType.HazardName : null,
                RiskLevel = r.HazardReports != null ? r.HazardReports.RiskLevel : null,
                ObservedAt = r.HazardReports != null ? r.HazardReports.ObservedAt : null,
                ExpectedClearanceAt = r.HazardReports != null ? r.HazardReports.ExpectedClearanceAt : null,
                Images = r.ReportImages
                    .OrderBy(i => i.UploadedAt)
                    .Select(i => new ReportImageDto(i.ImageId, i.Caption, i.UploadedAt))
                    .ToList()
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (report is null)
        {
            return null;
        }

        var isOwner = viewerUserId is not null && report.UserId == viewerUserId;
        var isPubliclyVisible = report.IsPublic == true && report.StatusCode == ReportStatusCodes.Verified;

        if (!isOwner && !viewerIsStaff && !isPubliclyVisible)
        {
            return null;
        }

        return new ReportDetailsDto(
            report.ReportId,
            report.ReportType,
            report.Title,
            report.Description,
            report.StatusCode,
            report.StatusName,
            report.StatusDescription,
            report.IsClosedStatus,
            report.ReportedAt,
            report.ResolvedAt,
            report.IsPublic == true,
            isOwner,
            // The reporter's real name is only shown to the owner and to moderation staff.
            isOwner || viewerIsStaff ? report.ReporterName ?? "Removed account" : "Community reporter",
            (double)report.Latitude,
            (double)report.Longitude,
            report.AddressLine,
            report.LandmarkName,
            report.AreaName,
            report.City,
            report.District,
            report.Images,
            report.AccidentTypeName,
            report.SeverityName,
            report.AccidentOccurredAt,
            report.NumberOfVehicles,
            report.NumberOfInjured,
            report.NumberOfDeaths,
            report.WeatherNotes,
            report.HazardTypeName,
            report.RiskLevel,
            report.ObservedAt,
            report.ExpectedClearanceAt);
    }

    public async Task<IReadOnlyList<MapReportDto>> GetPublicMapReportsAsync(
        MapBounds bounds,
        string? reportType,
        int limit,
        CancellationToken cancellationToken = default)
    {
        // Public layers only ever expose verified, public reports inside the requested bounds.
        var query = _db.Reports.AsNoTracking()
            .Where(r => r.IsPublic == true && r.Status.StatusCode == ReportStatusCodes.Verified)
            .Where(r => r.Location.Latitude >= (decimal)bounds.MinLat && r.Location.Latitude <= (decimal)bounds.MaxLat)
            .Where(r => r.Location.Longitude >= (decimal)bounds.MinLng && r.Location.Longitude <= (decimal)bounds.MaxLng);

        if (!string.IsNullOrWhiteSpace(reportType))
        {
            query = query.Where(r => r.ReportType == reportType);
        }

        return await query
            .OrderByDescending(r => r.ReportedAt)
            .Take(Math.Clamp(limit, 1, MaxMapReports))
            .Select(r => new MapReportDto(
                r.ReportId,
                r.ReportType,
                r.Title,
                (double)r.Location.Latitude,
                (double)r.Location.Longitude,
                r.Status.StatusCode,
                r.ReportedAt,
                r.Location.AreaName,
                r.AccidentReports != null ? r.AccidentReports.Severity.SeverityName : null,
                r.AccidentReports != null ? r.AccidentReports.AccidentType.TypeName : null,
                r.HazardReports != null ? r.HazardReports.HazardType.HazardName : null,
                r.HazardReports != null ? r.HazardReports.RiskLevel : null,
                r.ReportImages.OrderBy(i => i.UploadedAt).Select(i => (ulong?)i.ImageId).FirstOrDefault(),
                // Aggregate counts only; voter identities never leave the database.
                r.ReportVotes.Count(v => v.VoteType == ReportVoteTypes.Confirm),
                r.ReportVotes.Count(v => v.VoteType == ReportVoteTypes.Dispute)))
            .ToListAsync(cancellationToken);
    }

    public async Task<ReportImageFileDto?> GetImageForViewerAsync(
        ulong imageId,
        ulong? viewerUserId,
        bool viewerIsStaff,
        CancellationToken cancellationToken = default)
    {
        var image = await _db.ReportImages.AsNoTracking()
            .Where(i => i.ImageId == imageId)
            .Select(i => new
            {
                i.ImageUrl,
                i.Report.UserId,
                i.Report.IsPublic,
                i.Report.Status.StatusCode
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (image is null)
        {
            return null;
        }

        var isOwner = viewerUserId is not null && image.UserId == viewerUserId;
        var isPubliclyVisible = image.IsPublic == true && image.StatusCode == ReportStatusCodes.Verified;

        return isOwner || viewerIsStaff || isPubliclyVisible
            ? new ReportImageFileDto(image.ImageUrl)
            : null;
    }

    public async Task<IReadOnlyList<ReportVerificationEntryDto>> GetVerificationHistoryAsync(
        ulong reportId,
        bool includeNotes,
        CancellationToken cancellationToken = default) =>
        await _db.ReportVerifications.AsNoTracking()
            .Where(v => v.ReportId == reportId)
            .OrderBy(v => v.VerifiedAt)
            .ThenBy(v => v.VerificationId)
            .Select(v => new ReportVerificationEntryDto(
                v.VerificationId,
                v.Status.StatusCode,
                v.Status.StatusName,
                v.AdminUser != null ? v.AdminUser.FullName : null,
                includeNotes ? v.AdminComment : null,
                v.VerifiedAt))
            .ToListAsync(cancellationToken);

    private static System.Linq.Expressions.Expression<Func<Reports, ReportSummaryDto>> SummaryProjection =>
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
