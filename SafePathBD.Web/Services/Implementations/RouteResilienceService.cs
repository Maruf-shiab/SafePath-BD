using SafePathBD.Web.Models.DTOs.IntelligentRouting;
using SafePathBD.Web.Services.IntelligentRouting;
using SafePathBD.Web.Services.Interfaces;

namespace SafePathBD.Web.Services.Implementations;

public sealed class RouteResilienceService : IRouteResilienceService
{
    private readonly ITrafficDataRepository _trafficData;

    public RouteResilienceService(ITrafficDataRepository trafficData)
    {
        _trafficData = trafficData;
    }

    public double Calculate(
        JourneyPath path,
        SafetyExposureDto exposure,
        double totalDurationMinutes,
        int maxTransfers,
        int viableCandidateCount,
        DateTimeOffset departureTime)
    {
        var physical = path.Steps.Where(s => s.Edge is not null).ToArray();
        if (physical.Length == 0 || totalDurationMinutes <= 0)
        {
            return 0;
        }

        var bottleneck = physical.Max(s => s.DurationMinutes) / Math.Max(1d, physical.Sum(s => s.DurationMinutes)) * 100d;
        var elevatedExposure = exposure.ElevatedRiskMinutes / Math.Max(1d, totalDurationMinutes) * 100d;
        var transferFragility = path.TransferCount / (double)Math.Max(1, maxTransfers) * 100d;
        var volatility = physical
            .Where(s => !string.IsNullOrWhiteSpace(s.Edge!.TrafficRoadId))
            .Select(s => _trafficData.GetTrafficVolatility(s.Edge!.TrafficRoadId!, departureTime))
            .DefaultIfEmpty(50d)
            .Average();
        var alternativePenalty = viableCandidateCount switch
        {
            >= 3 => 0d,
            2 => 35d,
            _ => 75d
        };

        var penalty = bottleneck * 0.25d
            + elevatedExposure * 0.30d
            + Math.Min(100d, transferFragility) * 0.20d
            + volatility * 0.15d
            + alternativePenalty * 0.10d;
        return Math.Round(Math.Clamp(100d - penalty, 0d, 100d), 1);
    }
}
