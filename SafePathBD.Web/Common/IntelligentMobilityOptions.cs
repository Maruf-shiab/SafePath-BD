namespace SafePathBD.Web.Common;

public sealed class IntelligentMobilityOptions
{
    public const string SectionName = "IntelligentMobility";

    public string TrafficDatasetPath { get; set; } = "Data/Traffic/aust_10km_synthetic_traffic_development.csv";
    public string TrafficRoadMappingPath { get; set; } = "Data/Traffic/traffic_road_mapping.csv";
    public string TransitDataPath { get; set; } = "Data/Transit";
    public string TrafficModelPath { get; set; } = "ML/Models/traffic-congestion-model.zip";
    public string TrafficModelMetadataPath { get; set; } = "ML/Models/traffic-model-metadata.json";
    public string DefaultWeatherCondition { get; set; } = "Clear";
    public int TrafficPredictionCacheMinutes { get; set; } = 30;
    public double TrafficRoadMatchMaxMeters { get; set; } = 1200;
    public double GraphSampleMeters { get; set; } = 220;
    public double BusStopMatchMeters { get; set; } = 500;
    public int CandidatePoolSize { get; set; } = 12;
    public int TopJourneyCount { get; set; } = 3;
    public double DiversityThreshold { get; set; } = 0.90;
    public double BackupDiversityThreshold { get; set; } = 0.80;
    public int DefaultMaxTransfers { get; set; } = 2;
    public int DefaultMaxWalkingMeters { get; set; } = 1000;
    public int MaximumWalkingMeters { get; set; } = 2000;
    public double GenericTransferMinutes { get; set; } = 1.5;
    public double BusTransferMinutes { get; set; } = 2.0;
    public double BusStopDwellMinutes { get; set; } = 0.6;
    public double UnknownDataPenalty { get; set; } = 8.0;
    public double DepartureBenefitMinutes { get; set; } = 5.0;
    public double DepartureBenefitPercent { get; set; } = 0.08;
    public int IntelligentSearchCacheMinutes { get; set; } = 20;
    public int MaxDepartureHoursAhead { get; set; } = 168;
}

public static class MobilityModes
{
    public const string Walk = "WALK";
    public const string Rickshaw = "RICKSHAW";
    public const string Motorbike = "MOTORBIKE";
    public const string Bus = "BUS";
    public const string Car = "CAR";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        Walk, Rickshaw, Motorbike, Bus, Car
    };
}

public static class JourneyPreferences
{
    public const string Balanced = "BALANCED";
    public const string SafetyFirst = "SAFETY_FIRST";
    public const string FastestPractical = "FASTEST_PRACTICAL";
    public const string LessWalking = "LESS_WALKING";
    public const string FewerTransfers = "FEWER_TRANSFERS";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        Balanced, SafetyFirst, FastestPractical, LessWalking, FewerTransfers
    };
}

public static class TrafficLevels
{
    public const string Low = "LOW";
    public const string Moderate = "MODERATE";
    public const string Heavy = "HEAVY";
    public const string Severe = "SEVERE";

    public static double ClampIndex(double value) => Math.Clamp(value, 0d, 100d);

    public static string FromIndex(double value) => ClampIndex(value) switch
    {
        < 30d => Low,
        < 55d => Moderate,
        < 80d => Heavy,
        _ => Severe
    };
}

public sealed record JourneyRankingWeights(
    double Safety,
    double TravelTime,
    double Congestion,
    double Transfers,
    double Walking,
    double Distance,
    double Resilience)
{
    public double Total => Safety + TravelTime + Congestion + Transfers + Walking + Distance + Resilience;
}

public static class JourneyRankingPolicy
{
    public static JourneyRankingWeights For(string? preference) => preference?.Trim().ToUpperInvariant() switch
    {
        JourneyPreferences.SafetyFirst => new(60, 18, 8, 5, 3, 2, 4),
        JourneyPreferences.FastestPractical => new(30, 45, 8, 5, 4, 5, 3),
        JourneyPreferences.LessWalking => new(38, 24, 9, 7, 15, 3, 4),
        JourneyPreferences.FewerTransfers => new(40, 24, 8, 15, 5, 4, 4),
        _ => new(45, 25, 10, 8, 5, 3, 4)
    };
}
