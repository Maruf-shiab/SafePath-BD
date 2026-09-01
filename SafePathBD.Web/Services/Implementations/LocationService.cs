using Microsoft.EntityFrameworkCore;
using SafePathBD.Web.Data;
using SafePathBD.Web.Models.DTOs.Reports;
using SafePathBD.Web.Models.Entities;
using SafePathBD.Web.Services.Interfaces;

namespace SafePathBD.Web.Services.Implementations;

public sealed class LocationService : ILocationService
{
    // Coordinates are compared at 6 decimal places (~0.11 m). Anything closer than that is
    // treated as the same physical point and the existing row is reused; anything further
    // apart gets its own row, so genuinely distinct places are never merged.
    private const int MatchPrecision = 6;

    private const string DefaultCountry = "Bangladesh";

    private readonly SafePathDbContext _db;

    public LocationService(SafePathDbContext db)
    {
        _db = db;
    }

    public async Task<Locations> ResolveOrCreateAsync(ReportLocationInput input, CancellationToken cancellationToken = default)
    {
        var latitude = Math.Round((decimal)input.Latitude, MatchPrecision);
        var longitude = Math.Round((decimal)input.Longitude, MatchPrecision);

        // DECIMAL(10,7) in the database, so compare against a half-unit window at our precision.
        var tolerance = 0.5m / (decimal)Math.Pow(10, MatchPrecision);

        var existing = await _db.Locations
            .Where(l => l.Latitude >= latitude - tolerance && l.Latitude <= latitude + tolerance)
            .Where(l => l.Longitude >= longitude - tolerance && l.Longitude <= longitude + tolerance)
            .FirstOrDefaultAsync(cancellationToken);

        if (existing is not null)
        {
            // Fill in descriptive fields the stored row is missing, without overwriting known values.
            existing.AddressLine ??= Trim(input.AddressLine, 500);
            existing.LandmarkName ??= Trim(input.LandmarkName, 200);
            existing.AreaName ??= Trim(input.AreaName, 150);
            existing.City ??= Trim(input.City, 100);
            existing.District ??= Trim(input.District, 100);
            return existing;
        }

        var location = new Locations
        {
            Latitude = latitude,
            Longitude = longitude,
            AddressLine = Trim(input.AddressLine, 500),
            LandmarkName = Trim(input.LandmarkName, 200),
            AreaName = Trim(input.AreaName, 150),
            City = Trim(input.City, 100),
            District = Trim(input.District, 100),
            // country is NOT NULL with a database default; set it so the insert never depends on it.
            Country = DefaultCountry,
            PlaceProvider = NormalizeProvider(input.Provider),
            ExternalPlaceId = Trim(input.ExternalPlaceId, 255)
        };

        _db.Locations.Add(location);
        return location;
    }

    // locations.place_provider is an ENUM('GOOGLE','OSM','MANUAL','OTHER').
    private static string NormalizeProvider(string? provider) => provider?.Trim().ToUpperInvariant() switch
    {
        "OSM" => "OSM",
        "GOOGLE" => "GOOGLE",
        "MANUAL" => "MANUAL",
        null or "" => "MANUAL",
        _ => "OTHER"
    };

    private static string? Trim(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }
}
