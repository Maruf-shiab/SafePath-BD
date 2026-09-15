using System.Diagnostics;
using Microsoft.ML;
using SafePathBD.ML.Training.Models;

namespace SafePathBD.ML.Training.Training;

public sealed class ModelEvaluator
{
    private readonly MLContext _ml;
    public ModelEvaluator(MLContext ml) => _ml = ml;

    public (RegressionMetricsDto Regression, ClassMetricsDto Classes, double ClampRatePercent, double RowsPerSecond) Evaluate(ITransformer model, IReadOnlyList<TrafficTrainingRow> rows)
    {
        var data = _ml.Data.LoadFromEnumerable(rows);
        var sw = Stopwatch.StartNew();
        var transformed = model.Transform(data);
        var metrics = _ml.Regression.Evaluate(transformed, labelColumnName: "Label", scoreColumnName: "Score");
        var predictions = _ml.Data.CreateEnumerable<ScoredRow>(transformed, reuseRowObject: false).ToArray();
        sw.Stop();
        var classes = Classification(predictions);
        var clamp = predictions.Length == 0 ? 0d : predictions.Count(p => p.Score < 0 || p.Score > 100) * 100d / predictions.Length;
        return (
            new RegressionMetricsDto(metrics.MeanAbsoluteError, metrics.RootMeanSquaredError, metrics.RSquared),
            classes,
            clamp,
            predictions.Length / Math.Max(0.001, sw.Elapsed.TotalSeconds));
    }

    private static ClassMetricsDto Classification(IReadOnlyList<ScoredRow> rows)
    {
        var labels = new[] { "LOW", "MODERATE", "HEAVY", "SEVERE" };
        double correct = 0, precision = 0, recall = 0, f1 = 0;
        foreach (var label in labels)
        {
            var tp = rows.Count(r => TrafficFeatureBuilder.TrafficLevel(r.Label) == label && TrafficFeatureBuilder.TrafficLevel(Math.Clamp(r.Score, 0, 100)) == label);
            var fp = rows.Count(r => TrafficFeatureBuilder.TrafficLevel(r.Label) != label && TrafficFeatureBuilder.TrafficLevel(Math.Clamp(r.Score, 0, 100)) == label);
            var fn = rows.Count(r => TrafficFeatureBuilder.TrafficLevel(r.Label) == label && TrafficFeatureBuilder.TrafficLevel(Math.Clamp(r.Score, 0, 100)) != label);
            var p = tp + fp == 0 ? 0 : tp / (double)(tp + fp);
            var r = tp + fn == 0 ? 0 : tp / (double)(tp + fn);
            precision += p; recall += r; f1 += p + r == 0 ? 0 : 2 * p * r / (p + r);
        }
        correct = rows.Count(r => TrafficFeatureBuilder.TrafficLevel(r.Label) == TrafficFeatureBuilder.TrafficLevel(Math.Clamp(r.Score, 0, 100)));
        var n = Math.Max(1, rows.Count);
        return new ClassMetricsDto(correct / n, precision / 4d, recall / 4d, f1 / 4d);
    }

    private sealed class ScoredRow
    {
        public float Label { get; set; }
        public float Score { get; set; }
    }
}
