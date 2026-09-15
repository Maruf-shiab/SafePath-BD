using SafePathBD.Web.Models.DTOs.Routing;

namespace SafePathBD.Web.Services.Interfaces;

public sealed record SegmentSafetySnapshot(
    double? SafetyScore,
    string RiskClass,
    string HazardState,
    string Source,
    double Coverage,
    int RelevantIncidentCount,
    bool IsHardBlocked);

public interface ISegmentSafetyProvider
{
    Task<SegmentSafetySnapshot> GetSafetyAsync(
        string? trafficRoadId,
        IReadOnlyList<RouteCoordinate> geometry,
        IReadOnlyList<RouteIncidentDto> routeIncidents,
        CancellationToken cancellationToken = default);
}
