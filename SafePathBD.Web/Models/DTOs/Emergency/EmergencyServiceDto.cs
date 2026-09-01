namespace SafePathBD.Web.Models.DTOs.Emergency;

public sealed record EmergencyServiceTypeDto(ushort ServiceTypeId, string Name, string? Description);

public sealed record EmergencyServiceDto(
    ulong EmergencyServiceId,
    string ServiceName,
    string ServiceTypeName,
    double Latitude,
    double Longitude,
    string? AddressLine,
    string? LandmarkName,
    string? AreaName,
    string? City,
    string? District,
    string? Phone,
    string? EmergencyPhone,
    string? OpeningHours,
    bool Is24Hours,
    bool IsVerified,
    // Straight-line (Haversine) distance from the search origin, never travel distance.
    double? StraightLineDistanceKm);
