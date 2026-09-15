using SafePathBD.Web.Models.DTOs.Traffic;

namespace SafePathBD.Web.Services.Interfaces;

public interface IVehicleTravelTimeService
{
    Task<VehicleTravelTimeEstimate> EstimateAsync(
        string mode,
        double distanceMeters,
        TrafficPredictionRequest trafficRequest,
        string? busRouteId = null,
        bool includeBusWait = false,
        CancellationToken cancellationToken = default);
}

public interface IRickshawRoadEligibilityPolicy
{
    bool IsEligible(string? roadClass);
}
