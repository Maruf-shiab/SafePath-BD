using Microsoft.ML;
using SafePathBD.ML.Training.Models;
using SafePathBD.ML.Training.Reporting;
using SafePathBD.ML.Training.Training;

var repoRoot = FindRepoRoot(AppContext.BaseDirectory);
var dataset = args.Length > 0
    ? Path.GetFullPath(args[0])
    : Path.Combine(repoRoot, "SafePathBD.Web", "Data", "Traffic", "aust_10km_synthetic_traffic_development.csv");
var modelRoot = Path.Combine(repoRoot, "SafePathBD.Web", "ML", "Models");
var experimentRoot = Path.Combine(modelRoot, "experiments");
Directory.CreateDirectory(experimentRoot);

Console.WriteLine("SAFEPATH TRAFFIC MODEL EXPERIMENT");
Console.WriteLine(new string('=', 72));
var rows = TrafficDatasetLoader.Load(dataset);
if (rows.Count == 0) throw new InvalidOperationException("Traffic dataset contains no training rows.");
var split = DeterministicSplit.Primary(rows);
if (split.Train.Count == 0 || split.Validation.Count == 0 || split.Test.Count == 0)
    throw new InvalidOperationException("Deterministic primary split produced an empty partition.");

Console.WriteLine($"Dataset Rows: {rows.Count:N0}");
Console.WriteLine($"Train / Validation / Test: {split.Train.Count:N0} / {split.Validation.Count:N0} / {split.Test.Count:N0}");
Console.WriteLine($"Features: {string.Join(", ", TrafficFeatureBuilder.FeatureGroups)}");
Console.WriteLine($"Seed: {TrafficFeatureBuilder.Seed}");
Console.WriteLine();

TrafficFeatureBuilder.AssertLeakageSafeFeatureContract();

var ml = new MLContext(seed: TrafficFeatureBuilder.Seed);
var evaluator = new ModelEvaluator(ml);
var trainView = ml.Data.LoadFromEnumerable(split.Train);
var results = new List<ModelEvaluationResult>();
var fittedModels = new Dictionary<string, ITransformer>(StringComparer.OrdinalIgnoreCase);
var candidateSets = new[]
{
    ModelTrainers.FastTreeCandidates(ml),
    ModelTrainers.LightGbmCandidates(ml),
    ModelTrainers.SdcaCandidates(ml)
};

foreach (var familyCandidates in candidateSets)
{
    var family = familyCandidates[0].Family;
    Console.WriteLine(family);
    var candidateResults = new List<ModelEvaluationResult>();
    var candidateModels = new Dictionary<string, ITransformer>(StringComparer.Ordinal);

    foreach (var candidate in familyCandidates)
    {
        Console.WriteLine($"  Candidate: {candidate.Configuration}");
        var model = candidate.Pipeline.Fit(trainView);
        var validation = evaluator.Evaluate(model, split.Validation);
        var result = new ModelEvaluationResult
        {
            ModelName = family,
            Hyperparameters = candidate.Configuration,
            Validation = validation.Regression,
            ValidationClasses = validation.Classes,
            InferenceRowsPerSecond = validation.RowsPerSecond,
            ClampRatePercent = validation.ClampRatePercent
        };
        candidateResults.Add(result);
        candidateModels[candidate.Configuration] = model;
        Console.WriteLine($"    MAE={result.Validation.Mae:F4} RMSE={result.Validation.Rmse:F4} R²={result.Validation.R2:F4} Clamp={result.ClampRatePercent:F2}%");
    }

    var familyBest = ModelSelector.SelectBestConfiguration(candidateResults);
    var familyModel = candidateModels[familyBest.Hyperparameters];
    var artifact = Path.Combine(experimentRoot, family.ToLowerInvariant() + "-model.zip");
    ml.Model.Save(familyModel, trainView.Schema, artifact);
    familyBest.ArtifactPath = artifact;
    familyBest.ModelBytes = new FileInfo(artifact).Length;
    results.Add(familyBest);
    fittedModels[family] = familyModel;

    Console.WriteLine($"  Selected {family} configuration: {familyBest.Hyperparameters}");
    Print("  Validation", familyBest.Validation);
    Console.WriteLine($"  Validation Macro F1: {familyBest.ValidationClasses.MacroF1:F4}");
    Console.WriteLine();
}

var baselineValidation = BaselineEvaluator.Evaluate(split.Train, split.Validation);
var selected = ModelSelector.Select(results);
var improvement = baselineValidation.Mae - selected.Validation.Mae;
var improvementText = improvement > 0.05
    ? $" It improves validation MAE over the road-class/hour baseline by {improvement:F4}."
    : $" It does not materially improve validation MAE over the road-class/hour baseline ({baselineValidation.Mae:F4}); this limitation is reported rather than hidden.";
var selectionReason = $"Selected by lowest validation MAE ({selected.Validation.Mae:F4}); ties within {ModelSelector.MaeTieTolerance:F2} use lower validation RMSE, then higher R², then the simpler/faster model. Selected {selected.ModelName} configuration: {selected.Hyperparameters}.{improvementText}";
Console.WriteLine($"SELECTED MODEL: {selected.ModelName}");
Console.WriteLine($"Configuration: {selected.Hyperparameters}");
Console.WriteLine($"Selection reason: {selectionReason}");
Console.WriteLine();

// Production selection is now frozen. Test-set metrics below cannot influence the winner.
foreach (var result in results)
{
    var test = evaluator.Evaluate(fittedModels[result.ModelName], split.Test);
    result.Test = test.Regression;
    result.TestClasses = test.Classes;
    result.ClampRatePercent = Math.Max(result.ClampRatePercent, test.ClampRatePercent);
}
var finalTest = results.Single(r => r.ModelName == selected.ModelName);
Console.WriteLine("FINAL TEST — SELECTED PRODUCTION MODEL");
Print("Test", finalTest.Test!);
Console.WriteLine($"Test Macro F1: {finalTest.TestClasses!.MacroF1:F4}");
Console.WriteLine();

var holdout = DeterministicSplit.RoadHoldout(rows);
if (holdout.Train.Count == 0 || holdout.Test.Count == 0)
{
    throw new InvalidOperationException("Road-holdout robustness split produced an empty partition.");
}
foreach (var familyCandidates in candidateSets)
{
    var family = familyCandidates[0].Family;
    var chosen = results.Single(r => r.ModelName == family);
    var chosenPipeline = familyCandidates.Single(c => c.Configuration == chosen.Hyperparameters).Pipeline;
    var roadTrainView = ml.Data.LoadFromEnumerable(holdout.Train);
    var holdModel = chosenPipeline.Fit(roadTrainView);
    var metrics = evaluator.Evaluate(holdModel, holdout.Test);
    chosen.RoadHoldout = metrics.Regression;
    chosen.RoadHoldoutClasses = metrics.Classes;
}

var baselineTest = BaselineEvaluator.Evaluate(split.Train, split.Test);
var pfi = PermutationFeatureImportance.Calculate(ml, fittedModels[selected.ModelName], split.Validation, TrafficFeatureBuilder.Seed);

var canonical = Path.Combine(modelRoot, "traffic-congestion-model.zip");
ModelArtifactPublisher.PublishSelected(selected, canonical);
TrainingReportWriter.WriteAll(repoRoot, dataset, rows.Count, results, selected, baselineValidation, baselineTest, pfi, selectionReason);

Console.WriteLine($"Saved: {canonical}");
Console.WriteLine($"Metadata: {Path.Combine(modelRoot, "traffic-model-metadata.json")}");
Console.WriteLine($"Comparison: {Path.Combine(repoRoot, "docs", "TRAFFIC_MODEL_COMPARISON.md")}");

static void Print(string label, RegressionMetricsDto m)
{
    Console.WriteLine($"{label} MAE: {m.Mae:F4}");
    Console.WriteLine($"{label} RMSE: {m.Rmse:F4}");
    Console.WriteLine($"{label} R2: {m.R2:F4}");
}

static string FindRepoRoot(string start)
{
    var dir = new DirectoryInfo(start);
    while (dir is not null)
    {
        if (File.Exists(Path.Combine(dir.FullName, "SafePathBD.sln"))) return dir.FullName;
        dir = dir.Parent;
    }
    throw new DirectoryNotFoundException("Could not locate SafePathBD.sln from the training process path.");
}
