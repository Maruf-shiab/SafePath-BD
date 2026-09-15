using SafePathBD.ML.Training.Models;

namespace SafePathBD.ML.Training.Training;

public static class ModelSelector
{
    public const double MaeTieTolerance = 0.01;
    public const double MaximumAcceptableClampRatePercent = 20.0;
    private static readonly string[] SimplicityOrder = ["SDCA", "FASTTREE", "LIGHTGBM"];

    public static ModelEvaluationResult Select(IReadOnlyList<ModelEvaluationResult> results)
    {
        var tied = ValidationLeaders(results);
        return tied
            .OrderBy(r =>
            {
                var index = Array.IndexOf(SimplicityOrder, r.ModelName.ToUpperInvariant());
                return index < 0 ? int.MaxValue : index;
            })
            .ThenByDescending(r => r.InferenceRowsPerSecond)
            .First();
    }

    public static ModelEvaluationResult SelectBestConfiguration(IReadOnlyList<ModelEvaluationResult> candidates) =>
        ValidationLeaders(candidates)
            .OrderByDescending(r => r.InferenceRowsPerSecond)
            .ThenBy(r => r.ModelBytes)
            .First();

    private static List<ModelEvaluationResult> ValidationLeaders(IReadOnlyList<ModelEvaluationResult> results)
    {
        var valid = results.Where(IsValid).ToList();
        if (valid.Count == 0)
        {
            throw new InvalidOperationException("No valid traffic model evaluation was available for selection.");
        }

        // If at least one candidate has a plausible R², do not select a catastrophically worse
        // candidate merely because another metric happened to tie after clipping/noise.
        if (valid.Any(r => r.Validation.R2 > -0.25d))
        {
            valid = valid.Where(r => r.Validation.R2 >= -0.50d).ToList();
        }

        var minMae = valid.Min(r => r.Validation.Mae);
        var tied = valid.Where(r => r.Validation.Mae <= minMae + MaeTieTolerance).ToList();
        var bestRmse = tied.Min(r => r.Validation.Rmse);
        tied = tied.Where(r => r.Validation.Rmse <= bestRmse + MaeTieTolerance).ToList();
        var bestR2 = tied.Max(r => r.Validation.R2);
        return tied.Where(r => r.Validation.R2 >= bestR2 - 0.0001d).ToList();
    }

    private static bool IsValid(ModelEvaluationResult r) =>
        double.IsFinite(r.Validation.Mae)
        && double.IsFinite(r.Validation.Rmse)
        && double.IsFinite(r.Validation.R2)
        && r.Validation.Mae >= 0
        && r.Validation.Rmse >= 0
        && r.ClampRatePercent <= MaximumAcceptableClampRatePercent;
}
