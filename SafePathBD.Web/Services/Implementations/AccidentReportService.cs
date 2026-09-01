using Microsoft.EntityFrameworkCore;
using SafePathBD.Web.Common;
using SafePathBD.Web.Data;
using SafePathBD.Web.Models.DTOs.Reports;
using SafePathBD.Web.Models.Entities;
using SafePathBD.Web.Services.Interfaces;

namespace SafePathBD.Web.Services.Implementations;

public sealed class AccidentReportService : IAccidentReportService
{
    private readonly SafePathDbContext _db;
    private readonly ILocationService _locationService;
    private readonly ILogger<AccidentReportService> _logger;

    public AccidentReportService(SafePathDbContext db, ILocationService locationService, ILogger<AccidentReportService> logger)
    {
        _db = db;
        _locationService = locationService;
        _logger = logger;
    }

    public async Task<CreateReportResult> CreateAsync(
        CreateAccidentReportRequest request,
        IReadOnlyList<StoredImage> images,
        CancellationToken cancellationToken = default)
    {
        if (!GeoMath.IsValidCoordinate(request.Location.Latitude, request.Location.Longitude))
        {
            return new CreateReportResult(CreateReportStatus.InvalidLocation, Message: "The selected location is not valid.");
        }

        var typeExists = await _db.AccidentTypes.AsNoTracking()
            .AnyAsync(t => t.AccidentTypeId == request.AccidentTypeId, cancellationToken);
        var severityExists = await _db.AccidentSeverities.AsNoTracking()
            .AnyAsync(s => s.SeverityId == request.SeverityId, cancellationToken);

        if (!typeExists || !severityExists)
        {
            return new CreateReportResult(CreateReportStatus.InvalidLookup, Message: "Choose a valid accident type and severity.");
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
                ReportType = ReportTypes.Accident,
                UserId = request.UserId,
                LocationId = location.LocationId,
                // Road-segment matching is not implemented yet, so this stays NULL rather than being guessed.
                RoadSegmentId = null,
                StatusId = pendingStatusId.Value,
                Title = request.Title.Trim(),
                Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
                IsPublic = true
            };

            _db.Reports.Add(report);
            await _db.SaveChangesAsync(cancellationToken);

            _db.AccidentReports.Add(new AccidentReports
            {
                ReportId = report.ReportId,
                AccidentTypeId = request.AccidentTypeId,
                SeverityId = request.SeverityId,
                AccidentOccurredAt = request.AccidentOccurredAt,
                NumberOfVehicles = request.NumberOfVehicles,
                NumberOfInjured = request.NumberOfInjured,
                NumberOfDeaths = request.NumberOfDeaths,
                WeatherNotes = string.IsNullOrWhiteSpace(request.WeatherNotes) ? null : request.WeatherNotes.Trim()
            });

            ReportWorkflow.AddImages(_db, report.ReportId, images);
            await _db.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return new CreateReportResult(CreateReportStatus.Success, report.ReportId);
        }
        catch (DbUpdateException ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "Accident report creation failed and was rolled back.");
            return new CreateReportResult(CreateReportStatus.Failed,
                Message: "The report could not be saved. Please try again.");
        }
    }
}
