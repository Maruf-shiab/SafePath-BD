using SafePathBD.Web.Models.DTOs.Traffic;

namespace SafePathBD.Web.Services.Interfaces;

public interface ITrafficPredictionService
{
    bool ModelAvailable { get; }
    string ModelName { get; }
    string ModelVersion { get; }

    Task<TrafficPredictionResult> PredictAsync(
        TrafficPredictionRequest request,
        CancellationToken cancellationToken = default);
}

public interface ITrafficDataRepository
{
    int RowCount { get; }
    int RoadCount { get; }
    string DataStatus { get; }

    TrafficRoadMatch? FindNearestRoad(double latitude, double longitude);
    TrafficRoadProfile? GetRoad(string roadId);
    TrafficFallbackResult GetFallback(string roadId, DateTimeOffset at, string roadClass);
    double GetCalibratedSpeedKph(string mode, string roadClass, double predictedCongestionIndex);
    BusCalibration GetBusCalibration(string roadId, DateTimeOffset at);
    double GetTrafficVolatility(string roadId, DateTimeOffset at);
    ulong? GetMappedRoadSegmentId(string roadId);
}

public sealed record TrafficRoadProfile(
    string RoadId,
    string RoadName,
    string SegmentName,
    string Area,
    string RoadClass,
    double SegmentLengthKm,
    double CenterLatitude,
    double CenterLongitude,
    double DistanceFromAustKm,
    float RoadConditionScore);

public sealed record TrafficFallbackResult(double CongestionIndex, string Source, string Confidence);
public sealed record BusCalibration(bool Available, double ExpectedWaitMinutes);
