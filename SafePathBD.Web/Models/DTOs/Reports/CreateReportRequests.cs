namespace SafePathBD.Web.Models.DTOs.Reports;

/// <summary>Location captured on the map or resolved by the geocoder while filing a report.</summary>
public sealed record ReportLocationInput(
    double Latitude,
    double Longitude,
    string? AddressLine = null,
    string? LandmarkName = null,
    string? AreaName = null,
    string? City = null,
    string? District = null,
    string? Provider = null,
    string? ExternalPlaceId = null);

public sealed record CreateAccidentReportRequest(
    ulong UserId,
    string Title,
    string? Description,
    ReportLocationInput Location,
    ushort AccidentTypeId,
    byte SeverityId,
    DateTime? AccidentOccurredAt,
    ushort? NumberOfVehicles,
    ushort NumberOfInjured,
    ushort NumberOfDeaths,
    string? WeatherNotes);

public sealed record CreateHazardReportRequest(
    ulong UserId,
    string Title,
    string? Description,
    ReportLocationInput Location,
    ushort HazardTypeId,
    string RiskLevel,
    DateTime? ObservedAt,
    DateTime? ExpectedClearanceAt);

public enum CreateReportStatus
{
    Success,
    InvalidLocation,
    InvalidLookup,
    StatusConfigurationMissing,
    Failed
}

public sealed record CreateReportResult(CreateReportStatus Status, ulong ReportId = 0, string? Message = null)
{
    public bool Succeeded => Status == CreateReportStatus.Success;
}
