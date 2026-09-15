namespace SafePathBD.ML.Training.Models;

public sealed record RegressionMetricsDto(double Mae, double Rmse, double R2);
public sealed record ClassMetricsDto(double Accuracy, double MacroPrecision, double MacroRecall, double MacroF1);

public sealed class ModelEvaluationResult
{
    public required string ModelName { get; init; }
    public required RegressionMetricsDto Validation { get; init; }
    public required ClassMetricsDto ValidationClasses { get; init; }
    public RegressionMetricsDto? Test { get; set; }
    public ClassMetricsDto? TestClasses { get; set; }
    public RegressionMetricsDto? RoadHoldout { get; set; }
    public ClassMetricsDto? RoadHoldoutClasses { get; set; }
    public double InferenceRowsPerSecond { get; set; }
    public long ModelBytes { get; set; }
    public double ClampRatePercent { get; set; }
    public string ArtifactPath { get; set; } = string.Empty;
    public string Hyperparameters { get; set; } = string.Empty;
}

public sealed record DatasetSplit(
    IReadOnlyList<TrafficTrainingRow> Train,
    IReadOnlyList<TrafficTrainingRow> Validation,
    IReadOnlyList<TrafficTrainingRow> Test);
