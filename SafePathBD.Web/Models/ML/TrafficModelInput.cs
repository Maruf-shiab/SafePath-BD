using Microsoft.ML.Data;

namespace SafePathBD.Web.Models.ML;

/// <summary>
/// Runtime schema for the canonical ML.NET traffic model. The feature contract is kept
/// deliberately small and excludes all target-derived speed/travel-time fields.
/// </summary>
public sealed class TrafficModelInput
{
    public string RoadId { get; set; } = string.Empty;
    public string RoadClass { get; set; } = string.Empty;
    public string Area { get; set; } = string.Empty;
    public string DayType { get; set; } = string.Empty;
    public string WeatherCondition { get; set; } = string.Empty;
    public float HourSin { get; set; }
    public float HourCos { get; set; }
    public float DaySin { get; set; }
    public float DayCos { get; set; }
    public float RoadConditionScore { get; set; }
    public float DistanceFromAustKm { get; set; }

    [ColumnName("Label")]
    public float Label { get; set; }
}

public sealed class TrafficModelPrediction
{
    [ColumnName("Score")]
    public float Score { get; set; }
}

public static class TrafficFeatureEngineering
{
    public static (float Sin, float Cos) Cyclic(int value, int period)
    {
        var angle = 2d * Math.PI * value / period;
        return ((float)Math.Sin(angle), (float)Math.Cos(angle));
    }

    public static int DayIndex(DayOfWeek day) => day switch
    {
        DayOfWeek.Monday => 0,
        DayOfWeek.Tuesday => 1,
        DayOfWeek.Wednesday => 2,
        DayOfWeek.Thursday => 3,
        DayOfWeek.Friday => 4,
        DayOfWeek.Saturday => 5,
        DayOfWeek.Sunday => 6,
        _ => 0
    };

    public static string DayType(DayOfWeek day) => day is DayOfWeek.Friday or DayOfWeek.Saturday
        ? "Weekend"
        : "Weekday";
}
