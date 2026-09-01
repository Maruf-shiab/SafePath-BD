using Microsoft.EntityFrameworkCore;
using SafePathBD.Web.Common;
using SafePathBD.Web.Data;
using SafePathBD.Web.Models.DTOs.Reports;
using SafePathBD.Web.Models.Entities;
using SafePathBD.Web.Services.Interfaces;

namespace SafePathBD.Web.Services.Implementations;

public sealed class HazardReportService : IHazardReportService
{
    private readonly SafePathDbContext _db;
    private readonly ILocationService _locationService;
    private readonly ILogger<HazardReportService> _logger;

    public HazardReportService(SafePathDbContext db, ILocationService locationService, ILogger<HazardReportService> logger)
    {
        _db = db;
        _locationService = locationService;
        _logger = logger;
    }

    public async Task<CreateReportResult> CreateAsync(
        CreateHazardReportRequest request,
        IReadOnlyList<StoredImage> images,
        CancellationToken cancellationToken = default)
    {
        if (!GeoMath.IsValidCoordinate(request.Location.Latitude, request.Location.Longitude))
        {
            return new CreateReportResult(CreateReportStatus.InvalidLocation, Message: "The selected location is not valid.");
        }

        if (!HazardRiskLevels.IsValid(request.RiskLevel))
        {
            return new CreateReportResult(CreateReportStatus.InvalidLookup, Message: "Choose a valid risk level.");
        }

        var typeExists = await _db.HazardTypes.AsNoTracking()
            .AnyAsync(t => t.HazardTypeId == request.HazardTypeId, cancellationToken);

        if (!typeExists)
        {
            return new CreateReportResult(CreateReportStatus.InvalidLookup, Message: "Choose a valid hazard type.");
        }

        var pendingStatusId = await ReportWorkflow.GetPendingStatusIdAsync(_db, cancellationToken);
        if (pendingStatusId is null)
        {
            _logger.LogError("report_statuses is missing the {Code} row; reports cannot be created.", ReportStatusCodes.Pending);
            return new CreateReportResult(CreateReportStatus.StatusConfigurationMissing,
                Message: "Reporting is temporarily unavailable. Please try again later.");
        }

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var location = await _locationService.ResolveOrCreateAsync(request.Location, cancellationToken);
            await _db.SaveChangesAsync(cancellationToken);

            var report = new Reports
            {
                ReportType = ReportTypes.Hazard,
                UserId = request.UserId,
                LocationId = location.LocationId,
                RoadSegmentId = null,
                StatusId = pendingStatusId.Value,
                Title = request.Title.Trim(),
                Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
                IsPublic = true
            };

            _db.Reports.Add(report);
            await _db.SaveChangesAsync(cancellationToken);

            _db.HazardReports.Add(new HazardReports
            {
                ReportId = report.ReportId,
                HazardTypeId = request.HazardTypeId,
                RiskLevel = request.RiskLevel,
                ObservedAt = request.ObservedAt,
                ExpectedClearanceAt = request.ExpectedClearanceAt
            });

            ReportWorkflow.AddImages(_db, report.ReportId, images);
            await _db.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return new CreateReportResult(CreateReportStatus.Success, report.ReportId);
        }
        catch (DbUpdateException ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "Hazard report creation failed and was rolled back.");
            return new CreateReportResult(CreateReportStatus.Failed,
                Message: "The report could not be saved. Please try again.");
        }
    }
}
