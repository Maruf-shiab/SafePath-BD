using SafePathBD.Web.Common;
using SafePathBD.Web.Models.DTOs.Traffic;
using SafePathBD.Web.Services.Interfaces;

namespace SafePathBD.Web.Services.Implementations;

public sealed class VehicleTravelTimeService : IVehicleTravelTimeService
{
    private readonly ITrafficPredictionService _traffic;
    private readonly ITrafficDataRepository _data;
    private readonly ITransitNetworkService _transit;

    public VehicleTravelTimeService(
        ITrafficPredictionService traffic,
        ITrafficDataRepository data,
        ITransitNetworkService transit)
    {
        _traffic = traffic;
        _data = data;
        _transit = transit;
    }

    public async Task<VehicleTravelTimeEstimate> EstimateAsync(
        string mode,
        double distanceMeters,
        TrafficPredictionRequest trafficRequest,
        string? busRouteId = null,
        bool includeBusWait = false,
        CancellationToken cancellationToken = default)
    {
        var normalized = mode.Trim().ToUpperInvariant();
        var prediction = await _traffic.PredictAsync(trafficRequest, cancellationToken);
        var roadClass = prediction.RoadClass ?? trafficRequest.RoadClass ?? "secondary";
        var speed = normalized == MobilityModes.Walk
            ? _data.GetCalibratedSpeedKph(MobilityModes.Walk, roadClass, 40)
            : _data.GetCalibratedSpeedKph(normalized, roadClass, prediction.PredictedCongestionIndex);

        var available = true;
        var wait = 0d;
        string? note = null;

        if (normalized == MobilityModes.Bus)
        {
            var calibration = !string.IsNullOrWhiteSpace(prediction.TrafficRoadId)
                ? _data.GetBusCalibration(prediction.TrafficRoadId!, trafficRequest.At)
                : new BusCalibration(true, 0);
            available = calibration.Available || !string.IsNullOrWhiteSpace(busRouteId);
            if (includeBusWait && !string.IsNullOrWhiteSpace(busRouteId))
            {
                wait = _transit.GetExpectedWaitMinutes(busRouteId!, trafficRequest.At);
                if (wait <= 0)
                {
                    wait = calibration.ExpectedWaitMinutes;
                }
            }
            note = "Bus availability/wait is a synthetic development approximation.";
        }

        var travelMinutes = distanceMeters <= 0 || speed <= 0
            ? 0
            : (distanceMeters / 1000d) / speed * 60d;

        return new VehicleTravelTimeEstimate(
            normalized,
            distanceMeters,
            Math.Round(speed, 1),
            Math.Round(travelMinutes + wait, 2),
            prediction,
            available,
            Math.Round(wait, 1),
            note);
    }
}

public sealed class RickshawRoadEligibilityPolicy : IRickshawRoadEligibilityPolicy
{
    public bool IsEligible(string? roadClass) => roadClass?.Trim().ToUpperInvariant() switch
    {
        "TRUNK" => false,
        _ => true
    };
}
