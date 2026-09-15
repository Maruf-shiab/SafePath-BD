namespace SafePathBD.Web.Models.DTOs.Traffic;

public static class TrafficPredictionSources
{
    public const string MlModel = "ML_MODEL";
    public const string ExactRoadContextFallback = "EXACT_ROAD_CONTEXT_FALLBACK";
    public const string RoadHourFallback = "ROAD_HOUR_FALLBACK";
    public const string RoadClassHourFallback = "ROAD_CLASS_HOUR_FALLBACK";
    public const string GlobalHourFallback = "GLOBAL_HOUR_FALLBACK";
}


public static class TrafficPredictionPolicy
{
    public static double ClampIndex(double value) => double.IsFinite(value) ? Math.Clamp(value, 0d, 100d) : 50d;
}

public static class DataConfidenceLevels
{
    public const string High = "HIGH";
    public const string Medium = "MEDIUM";
    public const string Low = "LOW";
}

public sealed record TrafficRoadMatch(
    string? RoadId,
    string? RoadName,
    string? RoadClass,
    string? Area,
    double DistanceMeters,
    string Confidence,
    float RoadConditionScore,
    double DistanceFromAustKm);

public sealed record TrafficPredictionRequest(
    string? TrafficRoadId,
    double Latitude,
    double Longitude,
    DateTimeOffset At,
    string WeatherCondition,
    float? RoadConditionScore = null,
    string? RoadClass = null,
    string? Area = null,
    double? DistanceFromAustKm = null,
    double? MatchDistanceMeters = null);

public sealed record TrafficPredictionResult(
    double PredictedCongestionIndex,
    string TrafficLevel,
    string PredictionSource,
    string TrafficDataConfidence,
    string ModelName,
    string ModelVersion,
    string? TrafficRoadId,
    string? RoadName,
    string? RoadClass,
    double? MatchDistanceMeters);

public sealed record VehicleTravelTimeEstimate(
    string Mode,
    double DistanceMeters,
    double ExpectedSpeedKph,
    double TravelMinutes,
    TrafficPredictionResult Traffic,
    bool IsAvailable,
    double ExpectedWaitMinutes = 0,
    string? Note = null);
