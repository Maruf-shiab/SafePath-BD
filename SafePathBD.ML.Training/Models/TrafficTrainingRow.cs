using Microsoft.ML.Data;

namespace SafePathBD.ML.Training.Models;

public sealed class TrafficTrainingRow
{
    public string RecordId { get; set; } = string.Empty;
    public string RoadId { get; set; } = string.Empty;
    public string RoadClass { get; set; } = string.Empty;
    public string Area { get; set; } = string.Empty;
    public string DayType { get; set; } = string.Empty;
    public string WeatherCondition { get; set; } = string.Empty;
    public int DayIndex { get; set; }
    public int Hour { get; set; }
    public float HourSin { get; set; }
    public float HourCos { get; set; }
    public float DaySin { get; set; }
    public float DayCos { get; set; }
    public float RoadConditionScore { get; set; }
    public float DistanceFromAustKm { get; set; }

    [ColumnName("Label")]
    public float CongestionIndex { get; set; }
}

public sealed class TrafficPrediction
{
    [ColumnName("Score")]
    public float Score { get; set; }
}
