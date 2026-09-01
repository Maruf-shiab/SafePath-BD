using Microsoft.EntityFrameworkCore;
using SafePathBD.Web.Common;
using SafePathBD.Web.Data;
using SafePathBD.Web.Models.DTOs.Emergency;
using SafePathBD.Web.Models.Entities;
using SafePathBD.Web.Services.Interfaces;

namespace SafePathBD.Web.Services.Implementations;

public sealed class EmergencyService : IEmergencyService
{
    public const double MaxRadiusKm = 100;
    public const int MaxLimit = 100;

    private readonly SafePathDbContext _db;

    public EmergencyService(SafePathDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<EmergencyServiceTypeDto>> GetServiceTypesAsync(CancellationToken cancellationToken = default)
    {
        return await _db.EmergencyServiceTypes
            .AsNoTracking()
            .OrderBy(t => t.ServiceTypeId)
            .Select(t => new EmergencyServiceTypeDto(t.ServiceTypeId, t.ServiceTypeName, t.Description))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<EmergencyServiceDto>> GetNearbyAsync(
        double latitude,
        double longitude,
        string? serviceTypeName,
        double radiusKm,
        int limit,
        CancellationToken cancellationToken = default)
    {
        radiusKm = Math.Clamp(radiusKm, 0.1, MaxRadiusKm);
        limit = Math.Clamp(limit, 1, MaxLimit);

        var box = GeoMath.BoundingBox(latitude, longitude, radiusKm);

        // The bounding box narrows the rows in SQL using the lat/lng index; the exact
        // Haversine distance is then applied to that small set.
        var query = BaseQuery()
            .Where(s => s.Latitude >= (decimal)box.MinLat && s.Latitude <= (decimal)box.MaxLat)
            .Where(s => s.Longitude >= (decimal)box.MinLon && s.Longitude <= (decimal)box.MaxLon);

        if (!string.IsNullOrWhiteSpace(serviceTypeName))
        {
            query = query.Where(s => s.ServiceTypeName == serviceTypeName);
        }

        var candidates = await query.ToListAsync(cancellationToken);

        return RankByDistance(candidates, latitude, longitude, radiusKm, limit);
    }

    /// <summary>
    /// Applies the exact Haversine distance to the bounding-box candidates, drops anything
    /// outside the radius and orders the rest nearest-first.
    /// </summary>
    public static IReadOnlyList<EmergencyServiceDto> RankByDistance(
        IEnumerable<VwEmergencyServicesWithLocation> candidates,
        double latitude,
        double longitude,
        double radiusKm,
        int limit)
    {
        return candidates
            .Select(s => new
            {
                Service = s,
                DistanceKm = GeoMath.HaversineDistanceKm(latitude, longitude, (double)s.Latitude, (double)s.Longitude)
            })
            .Where(x => x.DistanceKm <= radiusKm)
            .OrderBy(x => x.DistanceKm)
            .ThenBy(x => x.Service.ServiceName)
            .Take(limit)
            .Select(x => Map(x.Service, x.DistanceKm))
            .ToList();
    }

    public async Task<EmergencyServiceDto?> GetByIdAsync(ulong emergencyServiceId, CancellationToken cancellationToken = default)
    {
        var service = await BaseQuery()
            .FirstOrDefaultAsync(s => s.EmergencyServiceId == emergencyServiceId, cancellationToken);

        return service is null ? null : Map(service, null);
    }

    private IQueryable<VwEmergencyServicesWithLocation> BaseQuery() =>
        _db.VwEmergencyServicesWithLocation
            .AsNoTracking()
            .Where(s => s.IsActive == true);

    private static EmergencyServiceDto Map(VwEmergencyServicesWithLocation s, double? distanceKm) =>
        new(
            s.EmergencyServiceId,
            s.ServiceName,
            s.ServiceTypeName,
            (double)s.Latitude,
            (double)s.Longitude,
            s.AddressLine,
            s.LandmarkName,
            s.AreaName,
            s.City,
            s.District,
            s.Phone,
            s.EmergencyPhone,
            s.OpeningHours,
            s.Is24Hours,
            s.IsVerified,
            distanceKm is null ? null : Math.Round(distanceKm.Value, 2));
}
