using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using SafePathBD.Web.Common;
using SafePathBD.Web.Data;
using SafePathBD.Web.Models.DTOs.Routing;
using SafePathBD.Web.Services.Interfaces;

namespace SafePathBD.Web.Services.Implementations;

/// <summary>
/// Safety abstraction for Chunk 7. It consumes an existing persisted segment score only when an
/// explicit traffic-road -> SafePath road-segment mapping exists. Otherwise it returns a clearly
/// identified incident-only development proxy instead of pretending the full Safety Score Engine
/// has been implemented.
/// </summary>
public sealed class SegmentSafetyProvider : ISegmentSafetyProvider
{
    private const double IncidentMatchMeters = 65d;
    private readonly SafePathDbContext _db;
    private readonly ITrafficDataRepository _trafficData;
    private readonly IMemoryCache _cache;

    public SegmentSafetyProvider(
        SafePathDbContext db,
        ITrafficDataRepository trafficData,
        IMemoryCache cache)
    {
        _db = db;
        _trafficData = trafficData;
        _cache = cache;
    }

    public async Task<SegmentSafetySnapshot> GetSafetyAsync(
        string? trafficRoadId,
        IReadOnlyList<RouteCoordinate> geometry,
        IReadOnlyList<RouteIncidentDto> routeIncidents,
        CancellationToken cancellationToken = default)
    {
        var relevant = routeIncidents
            .Where(incident => RouteGeometryMath.DistancePointToPolylineMeters(
                new RouteCoordinate(incident.Latitude, incident.Longitude), geometry) <= IncidentMatchMeters)
            .ToArray();

        var affected = relevant.Any(i => i.IncidentLevel == RouteIncidentStates.Affected);
        var caution = relevant.Any(i => i.IncidentLevel == RouteIncidentStates.Caution);
        var hardBlocked = relevant.Any(i =>
            i.ReportType == ReportTypes.Hazard
            && string.Equals(i.HazardType, "Road Block", StringComparison.OrdinalIgnoreCase)
            && string.Equals(i.RiskLevel, HazardRiskLevels.Critical, StringComparison.OrdinalIgnoreCase));
        var hazardState = affected ? RouteIncidentStates.Affected
            : caution ? RouteIncidentStates.Caution
            : RouteIncidentStates.Clear;

        double? persistedScore = null;
        string? persistedRisk = null;
        if (!string.IsNullOrWhiteSpace(trafficRoadId))
        {
            var segmentId = _trafficData.GetMappedRoadSegmentId(trafficRoadId!);
            if (segmentId.HasValue)
            {
                var cacheKey = $"safety-segment:{segmentId.Value}";
                var cached = await _cache.GetOrCreateAsync(cacheKey, async entry =>
                {
                    entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(15);
                    return await _db.SafetyScores.AsNoTracking()
                        .Where(s => s.RoadSegmentId == segmentId.Value)
                        .OrderByDescending(s => s.CalculatedAt)
                        .Select(s => new PersistedSafety((double)s.OverallSafetyScore, s.RiskLevel))
                        .FirstOrDefaultAsync(cancellationToken);
                });

                if (cached is not null)
                {
                    persistedScore = cached.Score;
                    persistedRisk = cached.Risk;
                }
            }
        }

        if (persistedScore.HasValue)
        {
            var adjusted = Math.Clamp(
                persistedScore.Value - (affected ? 25d : caution ? 10d : 0d),
                0d,
                100d);
            return new SegmentSafetySnapshot(
                Math.Round(adjusted, 1),
                RiskFromScore(adjusted, persistedRisk),
                hazardState,
                "DB_SAFETY_SCORE_PLUS_VERIFIED_INCIDENTS",
                1.0,
                relevant.Length,
                hardBlocked);
        }

        // This is deliberately a development proxy, not the final SafePath safety-score methodology.
        var proxy = affected ? 38d : caution ? 64d : 82d;
        return new SegmentSafetySnapshot(
            proxy,
            RiskFromScore(proxy, null),
            hazardState,
            "VERIFIED_INCIDENT_PROXY",
            0.45,
            relevant.Length,
            hardBlocked);
    }

    private static string RiskFromScore(double score, string? persistedRisk) => score switch
    {
        < 40 => "VERY_HIGH",
        < 60 => "HIGH",
        < 80 => "MODERATE",
        _ => "LOWER"
    };

    private sealed record PersistedSafety(double Score, string Risk);
}
