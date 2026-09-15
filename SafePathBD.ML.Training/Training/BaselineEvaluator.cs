using SafePathBD.ML.Training.Models;

namespace SafePathBD.ML.Training.Training;

public static class BaselineEvaluator
{
    public static RegressionMetricsDto Evaluate(IReadOnlyList<TrafficTrainingRow> train, IReadOnlyList<TrafficTrainingRow> test)
    {
        var means = train.GroupBy(r => (r.RoadClass.ToUpperInvariant(), r.Hour)).ToDictionary(g => g.Key, g => g.Average(x => (double)x.CongestionIndex));
        var global = train.Average(x => (double)x.CongestionIndex);
        var errors = test.Select(row =>
        {
            var pred = means.TryGetValue((row.RoadClass.ToUpperInvariant(), row.Hour), out var value) ? value : global;
            return (Actual: (double)row.CongestionIndex, Pred: pred);
        }).ToArray();
        var mae = errors.Average(x => Math.Abs(x.Actual - x.Pred));
        var rmse = Math.Sqrt(errors.Average(x => Math.Pow(x.Actual - x.Pred, 2)));
        var mean = errors.Average(x => x.Actual);
        var sse = errors.Sum(x => Math.Pow(x.Actual - x.Pred, 2));
        var sst = errors.Sum(x => Math.Pow(x.Actual - mean, 2));
        return new RegressionMetricsDto(mae, rmse, sst <= 1e-9 ? 0 : 1 - sse / sst);
    }
}
