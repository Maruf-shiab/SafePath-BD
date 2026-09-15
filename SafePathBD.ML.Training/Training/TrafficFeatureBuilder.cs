using Microsoft.ML;
using SafePathBD.ML.Training.Models;

namespace SafePathBD.ML.Training.Training;

public static class TrafficFeatureBuilder
{
    public const int Seed = 24091;

    public static readonly string[] FeatureGroups =
    [
        "RoadId", "RoadClass", "Area", "DayType", "WeatherCondition",
        "HourSin", "HourCos", "DaySin", "DayCos", "RoadConditionScore", "DistanceFromAustKm"
    ];

    public static readonly string[] SourceFeatureColumns =
    [
        "road_id", "road_class", "area", "day_type", "weather_condition",
        "hour", "day_of_week", "road_condition_score_0_100", "distance_from_aust_km"
    ];

    public static readonly string[] ExcludedLeakageFeatures =
    [
        "traffic_level", "db_traffic_level",
        "avg_speed_car_kph", "avg_speed_motorbike_kph", "avg_speed_rickshaw_kph", "avg_speed_bus_kph", "avg_speed_walk_kph",
        "travel_min_per_km_car", "travel_min_per_km_motorbike", "travel_min_per_km_rickshaw", "travel_min_per_km_bus", "travel_min_per_km_walk"
    ];


    public static void AssertLeakageSafeFeatureContract()
    {
        var forbidden = ExcludedLeakageFeatures.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var leaked = SourceFeatureColumns.Where(forbidden.Contains).ToArray();
        if (leaked.Length > 0)
        {
            throw new InvalidOperationException($"Target-derived leakage feature(s) entered the ML contract: {string.Join(", ", leaked)}");
        }
    }

    public static IEstimator<ITransformer> CreateSharedPreprocessing(MLContext ml)
    {
        return ml.Transforms.Categorical.OneHotEncoding(new[]
            {
                new InputOutputColumnPair("RoadIdEncoded", nameof(TrafficTrainingRow.RoadId)),
                new InputOutputColumnPair("RoadClassEncoded", nameof(TrafficTrainingRow.RoadClass)),
                new InputOutputColumnPair("AreaEncoded", nameof(TrafficTrainingRow.Area)),
                new InputOutputColumnPair("DayTypeEncoded", nameof(TrafficTrainingRow.DayType)),
                new InputOutputColumnPair("WeatherEncoded", nameof(TrafficTrainingRow.WeatherCondition))
            })
            .Append(ml.Transforms.Concatenate(
                "Features",
                "RoadIdEncoded", "RoadClassEncoded", "AreaEncoded", "DayTypeEncoded", "WeatherEncoded",
                nameof(TrafficTrainingRow.HourSin), nameof(TrafficTrainingRow.HourCos),
                nameof(TrafficTrainingRow.DaySin), nameof(TrafficTrainingRow.DayCos),
                nameof(TrafficTrainingRow.RoadConditionScore), nameof(TrafficTrainingRow.DistanceFromAustKm)));
    }

    public static TrafficTrainingRow WithTimeFeatures(TrafficTrainingRow row)
    {
        row.HourSin = (float)Math.Sin(2 * Math.PI * row.Hour / 24d);
        row.HourCos = (float)Math.Cos(2 * Math.PI * row.Hour / 24d);
        row.DaySin = (float)Math.Sin(2 * Math.PI * row.DayIndex / 7d);
        row.DayCos = (float)Math.Cos(2 * Math.PI * row.DayIndex / 7d);
        return row;
    }

    public static string TrafficLevel(double index) => index switch
    {
        < 30 => "LOW",
        < 55 => "MODERATE",
        < 80 => "HEAVY",
        _ => "SEVERE"
    };
}
