using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SafePathBD.Web.Common;
using SafePathBD.Web.Data;
using SafePathBD.Web.Models.DTOs.Reports;
using SafePathBD.Web.Models.ViewModels.Reports;
using SafePathBD.Web.Security;
using SafePathBD.Web.Services.Implementations;
using SafePathBD.Web.Services.Interfaces;

namespace SafePathBD.Web.Controllers;

public class ReportsController : Controller
{
    private readonly IReportService _reportService;
    private readonly IAccidentReportService _accidentReportService;
    private readonly IHazardReportService _hazardReportService;
    private readonly IReportImageService _imageService;
    private readonly IReportCommunityService _community;
    private readonly SafePathDbContext _db;

    public ReportsController(
        IReportService reportService,
        IAccidentReportService accidentReportService,
        IHazardReportService hazardReportService,
        IReportImageService imageService,
        IReportCommunityService community,
        SafePathDbContext db)
    {
        _reportService = reportService;
        _accidentReportService = accidentReportService;
        _hazardReportService = hazardReportService;
        _imageService = imageService;
        _community = community;
        _db = db;
    }

    // ----------------------------------------------------------------- accident

    [HttpGet("Reports/Accident")]
    [Authorize]
    public async Task<IActionResult> Accident(CancellationToken cancellationToken)
    {
        var model = new CreateAccidentReportViewModel { AccidentOccurredAt = DateTime.Now };
        await PopulateAsync(model, cancellationToken);
        return View(model);
    }

    [HttpPost("Reports/Accident")]
    [Authorize]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(25 * 1024 * 1024)]
    public async Task<IActionResult> Accident(CreateAccidentReportViewModel model, CancellationToken cancellationToken)
    {
        ValidateOccurredAt(model.AccidentOccurredAt, nameof(model.AccidentOccurredAt));

        var images = NormalizeFiles(model.Images);
        var imageCheck = _imageService.ValidateSet(images);
        if (!imageCheck.IsValid)
        {
            ModelState.AddModelError(nameof(model.Images), imageCheck.Error!);
        }

        if (!ModelState.IsValid)
        {
            await PopulateAsync(model, cancellationToken);
            return View(model);
        }

        var stored = await _imageService.SaveAsync(images, cancellationToken);

        var result = await _accidentReportService.CreateAsync(
            new CreateAccidentReportRequest(
                User.GetUserId(),
                model.Title,
                model.Description,
                model.Location.ToInput(),
                model.AccidentTypeId!.Value,
                model.SeverityId!.Value,
                model.AccidentOccurredAt,
                model.NumberOfVehicles,
                model.NumberOfInjured,
                model.NumberOfDeaths,
                model.WeatherNotes),
            stored,
            cancellationToken);

        if (!result.Succeeded)
        {
            _imageService.DeleteQuietly(stored);
            ModelState.AddModelError(string.Empty, result.Message ?? "The report could not be saved.");
            await PopulateAsync(model, cancellationToken);
            return View(model);
        }

        return RedirectToAction(nameof(Submitted), new { id = result.ReportId });
    }

    // ------------------------------------------------------------------- hazard

    [HttpGet("Reports/Hazard")]
    [Authorize]
    public async Task<IActionResult> Hazard(CancellationToken cancellationToken)
    {
        var model = new CreateHazardReportViewModel { ObservedAt = DateTime.Now };
        await PopulateAsync(model, cancellationToken);
        return View(model);
    }

    [HttpPost("Reports/Hazard")]
    [Authorize]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(25 * 1024 * 1024)]
    public async Task<IActionResult> Hazard(CreateHazardReportViewModel model, CancellationToken cancellationToken)
    {
        ValidateOccurredAt(model.ObservedAt, nameof(model.ObservedAt));

        if (!HazardRiskLevels.IsValid(model.RiskLevel))
        {
            ModelState.AddModelError(nameof(model.RiskLevel), "Choose a valid risk level.");
        }

        if (model.ExpectedClearanceAt is not null && model.ObservedAt is not null
            && model.ExpectedClearanceAt < model.ObservedAt)
        {
            ModelState.AddModelError(nameof(model.ExpectedClearanceAt), "Expected clearance cannot be before the hazard was seen.");
        }

        var images = NormalizeFiles(model.Images);
        var imageCheck = _imageService.ValidateSet(images);
        if (!imageCheck.IsValid)
        {
            ModelState.AddModelError(nameof(model.Images), imageCheck.Error!);
        }

        if (!ModelState.IsValid)
        {
            await PopulateAsync(model, cancellationToken);
            return View(model);
        }

        var stored = await _imageService.SaveAsync(images, cancellationToken);

        var result = await _hazardReportService.CreateAsync(
            new CreateHazardReportRequest(
                User.GetUserId(),
                model.Title,
                model.Description,
                model.Location.ToInput(),
                model.HazardTypeId!.Value,
                model.RiskLevel,
                model.ObservedAt,
                model.ExpectedClearanceAt),
            stored,
            cancellationToken);

        if (!result.Succeeded)
        {
            _imageService.DeleteQuietly(stored);
            ModelState.AddModelError(string.Empty, result.Message ?? "The report could not be saved.");
            await PopulateAsync(model, cancellationToken);
            return View(model);
        }

        return RedirectToAction(nameof(Submitted), new { id = result.ReportId });
    }

    // ------------------------------------------------------------------ reading

    [HttpGet("Reports/Submitted/{id:long}")]
    [Authorize]
    public async Task<IActionResult> Submitted(long id, CancellationToken cancellationToken)
    {
        var report = await _reportService.GetDetailsAsync((ulong)id, User.GetUserId(), IsStaff(), cancellationToken);
        return report is null ? NotFound() : View(report);
    }

    [HttpGet("Reports/My")]
    [Authorize]
    public async Task<IActionResult> My(
        string? type,
        string? status,
        int page = 1,
        CancellationToken cancellationToken = default)
    {
        // The owner id always comes from the cookie, never from the query string.
        var userId = User.GetUserId();

        var reports = await _reportService.GetMyReportsAsync(
            new MyReportsQuery(userId, NormalizeType(type), status, page), cancellationToken);

        var stats = await _reportService.GetMyReportStatsAsync(userId, cancellationToken);

        var statuses = await _db.ReportStatuses.AsNoTracking()
            .OrderBy(s => s.StatusId)
            .Select(s => new SelectListItem(s.StatusName, s.StatusCode, s.StatusCode == status))
            .ToListAsync(cancellationToken);

        return View(new MyReportsViewModel
        {
            Reports = reports,
            Stats = stats,
            ReportType = NormalizeType(type),
            StatusCode = status,
            StatusOptions = statuses
        });
    }

    [HttpGet("Reports/Details/{id:long}")]
    [AllowAnonymous]
    public async Task<IActionResult> Details(long id, CancellationToken cancellationToken)
    {
        var viewerId = User.Identity?.IsAuthenticated == true ? User.GetUserId() : (ulong?)null;

        var report = await _reportService.GetDetailsAsync((ulong)id, viewerId, IsStaff(), cancellationToken);
        if (report is null)
        {
            return NotFound();
        }

        var viewer = new CommunityViewer(viewerId, IsStaff());

        // Reviewer notes are internal context, so only the reporter and staff see them.
        var canSeeNotes = report.IsOwnedByViewer || IsStaff();

        return View(new ReportDetailsViewModel
        {
            Report = report,
            Votes = await _community.GetVoteSummaryAsync((ulong)id, viewer, cancellationToken),
            Comments = await _community.GetCommentsAsync((ulong)id, viewer, 1, ReportCommunityService.DefaultCommentPageSize, cancellationToken),
            History = await _reportService.GetVerificationHistoryAsync((ulong)id, canSeeNotes, cancellationToken),
            IsSignedIn = viewerId is > 0,
            ViewerIsStaff = IsStaff()
        });
    }

    /// <summary>
    /// Report images are stored outside wwwroot, so every read goes through the same
    /// visibility rule as the report itself.
    /// </summary>
    [HttpGet("Reports/Image/{id:long}")]
    [AllowAnonymous]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public async Task<IActionResult> Image(long id, CancellationToken cancellationToken)
    {
        if (id <= 0)
        {
            return NotFound();
        }

        var viewerId = User.Identity?.IsAuthenticated == true ? User.GetUserId() : (ulong?)null;

        var image = await _reportService.GetImageForViewerAsync((ulong)id, viewerId, IsStaff(), cancellationToken);
        if (image is null)
        {
            return NotFound();
        }

        var physicalPath = _imageService.ResolvePhysicalPath(image.StorageKey);
        return physicalPath is null
            ? NotFound()
            : PhysicalFile(physicalPath, _imageService.GetContentType(image.StorageKey));
    }

    // ------------------------------------------------------------------ helpers

    private bool IsStaff() => User.IsInRole(RoleNames.Admin) || User.IsInRole(RoleNames.Moderator);

    private static string? NormalizeType(string? type) => type?.ToUpperInvariant() switch
    {
        ReportTypes.Accident => ReportTypes.Accident,
        ReportTypes.Hazard => ReportTypes.Hazard,
        _ => null
    };

    private static List<IFormFile> NormalizeFiles(IEnumerable<IFormFile>? files) =>
        files?.Where(f => f is { Length: > 0 }).ToList() ?? new List<IFormFile>();

    private void ValidateOccurredAt(DateTime? value, string field)
    {
        if (value is null)
        {
            return;
        }

        // A small tolerance covers clock skew between the browser and the server.
        if (value > DateTime.Now.AddMinutes(5))
        {
            ModelState.AddModelError(field, "This cannot be in the future.");
        }
        else if (value < DateTime.Now.AddYears(-5))
        {
            ModelState.AddModelError(field, "This is too far in the past to report.");
        }
    }

    private async Task PopulateAsync(CreateReportViewModelBase model, CancellationToken cancellationToken)
    {
        var lookups = await _reportService.GetLookupsAsync(cancellationToken);

        model.MaxImages = _imageService.MaxImagesPerReport;
        model.MaxImageMegabytes = (int)(_imageService.MaxBytesPerImage / (1024 * 1024));

        switch (model)
        {
            case CreateAccidentReportViewModel accident:
                accident.AccidentTypes = lookups.AccidentTypes;
                accident.Severities = lookups.AccidentSeverities;
                break;

            case CreateHazardReportViewModel hazard:
                hazard.HazardTypes = lookups.HazardTypes;
                break;
        }
    }
}
