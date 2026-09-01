using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Mvc.Rendering;
using SafePathBD.Web.Common;
using SafePathBD.Web.Models.DTOs.Reports;

namespace SafePathBD.Web.Models.ViewModels.Reports;

/// <summary>Location fields shared by both report forms, populated by the map picker.</summary>
public class ReportLocationViewModel
{
    [Required(ErrorMessage = "Choose a location on the map.")]
    [Range(-90, 90, ErrorMessage = "Latitude must be between -90 and 90.")]
    public double? Latitude { get; set; }

    [Required(ErrorMessage = "Choose a location on the map.")]
    [Range(-180, 180, ErrorMessage = "Longitude must be between -180 and 180.")]
    public double? Longitude { get; set; }

    [StringLength(500)]
    public string? AddressLine { get; set; }

    [StringLength(200)]
    public string? LandmarkName { get; set; }

    [StringLength(150)]
    public string? AreaName { get; set; }

    [StringLength(100)]
    public string? City { get; set; }

    [StringLength(100)]
    public string? District { get; set; }

    [StringLength(20)]
    public string? Provider { get; set; }

    [StringLength(255)]
    public string? ExternalPlaceId { get; set; }

    public ReportLocationInput ToInput() => new(
        Latitude ?? 0,
        Longitude ?? 0,
        AddressLine,
        LandmarkName,
        AreaName,
        City,
        District,
        Provider,
        ExternalPlaceId);
}

public abstract class CreateReportViewModelBase
{
    [Required(ErrorMessage = "Give your report a short title.")]
    [StringLength(200, MinimumLength = 5, ErrorMessage = "The title must be between 5 and 200 characters.")]
    public string Title { get; set; } = string.Empty;

    [StringLength(2000, ErrorMessage = "The description cannot be longer than 2000 characters.")]
    public string? Description { get; set; }

    public ReportLocationViewModel Location { get; set; } = new();

    // Optional: a non-nullable collection would be treated as a required field by the binder.
    [ValidateNever]
    public List<IFormFile>? Images { get; set; }

    public int MaxImages { get; set; } = 4;

    public int MaxImageMegabytes { get; set; } = 5;
}

public class CreateAccidentReportViewModel : CreateReportViewModelBase
{
    [Required(ErrorMessage = "Choose the type of accident.")]
    public ushort? AccidentTypeId { get; set; }

    [Required(ErrorMessage = "Choose how severe the accident was.")]
    public byte? SeverityId { get; set; }

    [Required(ErrorMessage = "Enter when the accident happened.")]
    [DataType(DataType.DateTime)]
    [DisplayFormat(DataFormatString = "{0:yyyy-MM-ddTHH:mm}", ApplyFormatInEditMode = true)]
    public DateTime? AccidentOccurredAt { get; set; }

    [Range(0, 200, ErrorMessage = "Enter a number of vehicles between 0 and 200.")]
    public ushort? NumberOfVehicles { get; set; }

    [Range(0, 500, ErrorMessage = "Enter a number of injured people between 0 and 500.")]
    public ushort NumberOfInjured { get; set; }

    [Range(0, 500, ErrorMessage = "Enter a number of deaths between 0 and 500.")]
    public ushort NumberOfDeaths { get; set; }

    [StringLength(255, ErrorMessage = "Weather notes cannot be longer than 255 characters.")]
    public string? WeatherNotes { get; set; }

    public IReadOnlyList<ReportLookupItem> AccidentTypes { get; set; } = Array.Empty<ReportLookupItem>();

    public IReadOnlyList<ReportLookupItem> Severities { get; set; } = Array.Empty<ReportLookupItem>();
}

public class CreateHazardReportViewModel : CreateReportViewModelBase
{
    [Required(ErrorMessage = "Choose the type of hazard.")]
    public ushort? HazardTypeId { get; set; }

    [Required(ErrorMessage = "Choose a risk level.")]
    public string RiskLevel { get; set; } = HazardRiskLevels.Moderate;

    [Required(ErrorMessage = "Enter when you saw the hazard.")]
    [DataType(DataType.DateTime)]
    [DisplayFormat(DataFormatString = "{0:yyyy-MM-ddTHH:mm}", ApplyFormatInEditMode = true)]
    public DateTime? ObservedAt { get; set; }

    [DataType(DataType.DateTime)]
    [DisplayFormat(DataFormatString = "{0:yyyy-MM-ddTHH:mm}", ApplyFormatInEditMode = true)]
    public DateTime? ExpectedClearanceAt { get; set; }

    public IReadOnlyList<ReportLookupItem> HazardTypes { get; set; } = Array.Empty<ReportLookupItem>();
}

public class MyReportsViewModel
{
    public PagedResult<ReportSummaryDto> Reports { get; init; } = new(Array.Empty<ReportSummaryDto>(), 1, 20, 0);

    public MyReportStatsDto Stats { get; init; } = new(0, 0, 0, 0, 0);

    public string? ReportType { get; init; }

    public string? StatusCode { get; init; }

    public IReadOnlyList<SelectListItem> StatusOptions { get; init; } = Array.Empty<SelectListItem>();
}

public class ReportDetailsViewModel
{
    public ReportDetailsDto Report { get; init; } = null!;

    public ReportVoteSummaryDto? Votes { get; init; }

    public PagedResult<ReportCommentDto> Comments { get; init; } =
        new(Array.Empty<ReportCommentDto>(), 1, 10, 0);

    /// <summary>Official moderation history. Notes are only included for the owner and staff.</summary>
    public IReadOnlyList<ReportVerificationEntryDto> History { get; init; } = Array.Empty<ReportVerificationEntryDto>();

    public bool IsSignedIn { get; init; }

    public bool ViewerIsStaff { get; init; }
}
