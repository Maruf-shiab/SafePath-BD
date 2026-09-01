namespace SafePathBD.Web.Models.DTOs.Locations;

/// <summary>Provider-neutral place suggestion returned by location search.</summary>
public sealed record PlaceSuggestionDto(
    string DisplayName,
    string ShortName,
    double Latitude,
    double Longitude,
    string Provider,
    string? ExternalPlaceId);

/// <summary>Provider-neutral result of resolving coordinates back to a place.</summary>
public sealed record ResolvedPlaceDto(
    double Latitude,
    double Longitude,
    string DisplayName,
    string? AddressLine,
    string? AreaName,
    string? City,
    string? District,
    string Provider);
