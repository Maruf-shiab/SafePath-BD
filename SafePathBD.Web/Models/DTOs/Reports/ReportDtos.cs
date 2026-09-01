namespace SafePathBD.Web.Models.DTOs.Reports;

public sealed record ReportLookupItem(int Id, string Name, string? Description = null);

/// <summary>Lookup values loaded from the database for the report forms.</summary>
public sealed record ReportLookupsDto(
    IReadOnlyList<ReportLookupItem> AccidentTypes,
    IReadOnlyList<ReportLookupItem> AccidentSeverities,
    IReadOnlyList<ReportLookupItem> HazardTypes);

/// <summary>Compact projection used by report lists and cards.</summary>
public sealed record ReportSummaryDto(
    ulong ReportId,
    string ReportType,
    string Title,
    string StatusCode,
    string StatusName,
    DateTime ReportedAt,
    bool IsPublic,
    double Latitude,
    double Longitude,
    string? AreaName,
    string? City,
    string? SeverityName,
    string? AccidentTypeName,
    string? HazardTypeName,
    string? RiskLevel,
    ulong? ThumbnailImageId,
    int ImageCount);

public sealed record ReportImageDto(ulong ImageId, string? Caption, DateTime UploadedAt);

/// <summary>Full report projection. Never carries reporter contact details.</summary>
public sealed record ReportDetailsDto(
    ulong ReportId,
    string ReportType,
    string Title,
    string? Description,
    string StatusCode,
    string StatusName,
    string? StatusDescription,
    bool IsClosedStatus,
    DateTime ReportedAt,
    DateTime? ResolvedAt,
    bool IsPublic,
    bool IsOwnedByViewer,
    string ReporterName,
    double Latitude,
    double Longitude,
    string? AddressLine,
    string? LandmarkName,
    string? AreaName,
    string? City,
    string? District,
    IReadOnlyList<ReportImageDto> Images,
    // Accident-only
    string? AccidentTypeName,
    string? SeverityName,
    DateTime? AccidentOccurredAt,
    ushort? NumberOfVehicles,
    ushort? NumberOfInjured,
    ushort? NumberOfDeaths,
    string? WeatherNotes,
    // Hazard-only
    string? HazardTypeName,
    string? RiskLevel,
    DateTime? ObservedAt,
    DateTime? ExpectedClearanceAt);

/// <summary>Map-safe report projection. Deliberately excludes any reporter identity.</summary>
public sealed record MapReportDto(
    ulong ReportId,
    string ReportType,
    string Title,
    double Latitude,
    double Longitude,
    string StatusCode,
    DateTime ReportedAt,
    string? AreaName,
    string? SeverityName,
    string? AccidentTypeName,
    string? HazardTypeName,
    string? RiskLevel,
    ulong? ThumbnailImageId,
    int ConfirmCount,
    int DisputeCount);

public sealed record MyReportStatsDto(int Total, int Pending, int Verified, int Resolved, int Rejected);

/// <summary>Everything the image endpoint needs to serve a file after an access check.</summary>
public sealed record ReportImageFileDto(string StorageKey);
