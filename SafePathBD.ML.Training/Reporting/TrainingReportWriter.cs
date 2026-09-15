using System.Globalization;
using System.Text;
using System.Text.Json;
using SafePathBD.ML.Training.Models;
using SafePathBD.ML.Training.Training;

namespace SafePathBD.ML.Training.Reporting;

public static class TrainingReportWriter
{
    public static void WriteAll(
        string repoRoot,
        string datasetFile,
        int datasetRows,
        IReadOnlyList<ModelEvaluationResult> results,
        ModelEvaluationResult selected,
        RegressionMetricsDto baselineValidation,
        RegressionMetricsDto baselineTest,
        IReadOnlyDictionary<string,double> importance,
        string selectionReason)
    {
        var modelDir = Path.Combine(repoRoot, "SafePathBD.Web", "ML", "Models");
        Directory.CreateDirectory(modelDir);
        var comparisonPath = Path.Combine(modelDir, "traffic-model-comparison.csv");
        using (var writer = new StreamWriter(comparisonPath, false, Encoding.UTF8))
        {
            writer.WriteLine("model,hyperparameters,validation_mae,validation_rmse,validation_r2,test_mae,test_rmse,test_r2,test_macro_f1,road_holdout_mae,road_holdout_rmse,road_holdout_r2,inference_rows_per_second,model_bytes,clamp_rate_percent");
            foreach (var r in results)
            {
                writer.WriteLine(string.Join(',', new[]
                {
                    r.ModelName, Csv(r.Hyperparameters),
                    F(r.Validation.Mae),F(r.Validation.Rmse),F(r.Validation.R2),
                    F(r.Test?.Mae),F(r.Test?.Rmse),F(r.Test?.R2),F(r.TestClasses?.MacroF1),
                    F(r.RoadHoldout?.Mae),F(r.RoadHoldout?.Rmse),F(r.RoadHoldout?.R2),
                    F(r.InferenceRowsPerSecond),r.ModelBytes.ToString(CultureInfo.InvariantCulture),F(r.ClampRatePercent)
                }));
            }
        }

        var metadata = new
        {
            selectedModel = selected.ModelName,
            selectedHyperparameters = selected.Hyperparameters,
            modelVersion = "1.0",
            trainedAtUtc = DateTime.UtcNow,
            datasetFile = Path.GetFileName(datasetFile),
            datasetRows,
            target = "congestion_index_0_100",
            features = TrafficFeatureBuilder.FeatureGroups,
            excludedLeakageFeatures = TrafficFeatureBuilder.ExcludedLeakageFeatures,
            splitMethodology = "Deterministic FNV-1a hash of road_id|day_index|hour: 70% train, 15% validation, 15% test; separate deterministic road-id holdout robustness evaluation.",
            validationMetrics = selected.Validation,
            testMetrics = selected.Test,
            roadHoldoutMetrics = selected.RoadHoldout,
            baselineValidation,
            baselineTest,
            permutationFeatureImportanceMaeIncrease = importance,
            fallbackPolicy = new[] { "exact road + hour + day type mean", "road + hour mean", "road class + hour mean", "global hourly mean" },
            selectedBecause = selectionReason,
            outperformedValidationBaseline = selected.Validation.Mae < baselineValidation.Mae,
            validationMaeImprovementVsBaseline = baselineValidation.Mae - selected.Validation.Mae,
            syntheticData = true,
            randomSeed = TrafficFeatureBuilder.Seed
        };
        File.WriteAllText(Path.Combine(modelDir, "traffic-model-metadata.json"), JsonSerializer.Serialize(metadata, new JsonSerializerOptions { WriteIndented = true }));
        File.WriteAllText(Path.Combine(modelDir, "traffic-model-metrics.json"), JsonSerializer.Serialize(new { models = results, baselineValidation, baselineTest }, new JsonSerializerOptions { WriteIndented = true }));

        var docsPath = Path.Combine(repoRoot, "docs", "TRAFFIC_MODEL_COMPARISON.md");
        var md = new StringBuilder();
        md.AppendLine("# SafePath BD Traffic Model Comparison").AppendLine();
        md.AppendLine("> Development experiment over the supplied **synthetic** AUST 10 km traffic dataset. These metrics do not represent measured live Dhaka traffic.").AppendLine();
        md.AppendLine("| Model | Selected configuration | Val MAE | Val RMSE | Val R² | Test MAE | Test RMSE | Test R² | Test Macro F1 | Road-holdout MAE |");
        md.AppendLine("|---|---|---:|---:|---:|---:|---:|---:|---:|---:|");
        foreach (var r in results)
            md.AppendLine($"| {r.ModelName} | `{r.Hyperparameters}` | {N(r.Validation.Mae)} | {N(r.Validation.Rmse)} | {N(r.Validation.R2)} | {N(r.Test?.Mae)} | {N(r.Test?.Rmse)} | {N(r.Test?.R2)} | {N(r.TestClasses?.MacroF1)} | {N(r.RoadHoldout?.Mae)} |");
        md.AppendLine($"| Baseline (road-class/hour mean) | — | {N(baselineValidation.Mae)} | {N(baselineValidation.Rmse)} | {N(baselineValidation.R2)} | {N(baselineTest.Mae)} | {N(baselineTest.Rmse)} | {N(baselineTest.R2)} | — | — |");
        md.AppendLine().AppendLine($"## SELECTED PRODUCTION MODEL: {selected.ModelName}").AppendLine();
        md.AppendLine(selectionReason).AppendLine();
        var delta = baselineValidation.Mae - selected.Validation.Mae;
        md.AppendLine(selected.Validation.Mae < baselineValidation.Mae
            ? $"The selected ML model improves validation MAE over the road-class/hour baseline by **{delta:F3}** points."
            : $"The selected ML model does **not** improve validation MAE over the road-class/hour baseline (difference: {delta:F3}). This result should be reported honestly rather than claiming an ML advantage.").AppendLine();
        md.AppendLine("## Permutation feature importance").AppendLine();
        md.AppendLine("MAE increase after independently permuting each raw feature group. Importance is predictive association, **not causal proof**.").AppendLine();
        foreach (var item in importance.Take(12)) md.AppendLine($"- **{item.Key}**: +{item.Value:F3} MAE");
        File.WriteAllText(docsPath, md.ToString());
    }

    private static string Csv(string value) => '"' + (value ?? string.Empty).Replace("\"", "\"\"") + '"';
    private static string F(double? value) => value.HasValue ? value.Value.ToString("0.######", CultureInfo.InvariantCulture) : string.Empty;
    private static string N(double? value) => value.HasValue ? value.Value.ToString("0.###", CultureInfo.InvariantCulture) : "—";
}
