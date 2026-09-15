using Microsoft.ML;
using Microsoft.ML.Trainers;
using Microsoft.ML.Trainers.FastTree;
using Microsoft.ML.Trainers.LightGbm;

namespace SafePathBD.ML.Training.Training;

public sealed record TrainerCandidate(
    string Family,
    string Configuration,
    IEstimator<ITransformer> Pipeline);

public static class ModelTrainers
{
    // The parameter grids are intentionally small. Chunk 7 requires a defensible academic
    // comparison, not a costly AutoML sweep. Every configuration uses the exact same records.
    public static IReadOnlyList<TrainerCandidate> FastTreeCandidates(MLContext ml) =>
    [
        new("FASTTREE", "leaves=24;trees=180;minLeaf=10;learningRate=0.06", FastTree(ml, 24, 180, 10, 0.06)),
        new("FASTTREE", "leaves=32;trees=220;minLeaf=12;learningRate=0.05", FastTree(ml, 32, 220, 12, 0.05)),
        new("FASTTREE", "leaves=48;trees=260;minLeaf=16;learningRate=0.035", FastTree(ml, 48, 260, 16, 0.035))
    ];

    public static IReadOnlyList<TrainerCandidate> LightGbmCandidates(MLContext ml) =>
    [
        new("LIGHTGBM", "leaves=24;iterations=200;minLeaf=10;learningRate=0.06", LightGbm(ml, 24, 200, 10, 0.06)),
        new("LIGHTGBM", "leaves=31;iterations=240;minLeaf=12;learningRate=0.05", LightGbm(ml, 31, 240, 12, 0.05)),
        new("LIGHTGBM", "leaves=40;iterations=280;minLeaf=16;learningRate=0.035", LightGbm(ml, 40, 280, 16, 0.035))
    ];

    public static IReadOnlyList<TrainerCandidate> SdcaCandidates(MLContext ml) =>
    [
        new("SDCA", "iterations=150;normalized=true;threads=1;shuffle=false", Sdca(ml))
    ];

    // Public defaults are retained for focused unit tests and simple experimentation.
    public static IEstimator<ITransformer> FastTree(MLContext ml) => FastTree(ml, 32, 220, 12, 0.05);

    public static IEstimator<ITransformer> LightGbm(MLContext ml) => LightGbm(ml, 31, 240, 12, 0.05);

    public static IEstimator<ITransformer> Sdca(MLContext ml) =>
        TrafficFeatureBuilder.CreateSharedPreprocessing(ml)
            .Append(ml.Transforms.NormalizeMeanVariance("Features"))
            .Append(ml.Regression.Trainers.Sdca(new SdcaRegressionTrainer.Options
            {
                LabelColumnName = "Label",
                FeatureColumnName = "Features",
                MaximumNumberOfIterations = 150,
                Shuffle = false,
                NumberOfThreads = 1
            }));

    private static IEstimator<ITransformer> FastTree(
        MLContext ml,
        int leaves,
        int trees,
        int minimumLeaf,
        double learningRate) =>
        TrafficFeatureBuilder.CreateSharedPreprocessing(ml)
            .Append(ml.Regression.Trainers.FastTree(new FastTreeRegressionTrainer.Options
            {
                LabelColumnName = "Label",
                FeatureColumnName = "Features",
                NumberOfLeaves = leaves,
                NumberOfTrees = trees,
                MinimumExampleCountPerLeaf = minimumLeaf,
                LearningRate = learningRate,
                Seed = TrafficFeatureBuilder.Seed,
                NumberOfThreads = 1
            }));

    private static IEstimator<ITransformer> LightGbm(
        MLContext ml,
        int leaves,
        int iterations,
        int minimumLeaf,
        double learningRate) =>
        TrafficFeatureBuilder.CreateSharedPreprocessing(ml)
            .Append(ml.Regression.Trainers.LightGbm(new LightGbmRegressionTrainer.Options
            {
                LabelColumnName = "Label",
                FeatureColumnName = "Features",
                NumberOfLeaves = leaves,
                NumberOfIterations = iterations,
                MinimumExampleCountPerLeaf = minimumLeaf,
                LearningRate = learningRate,
                Seed = TrafficFeatureBuilder.Seed,
                NumberOfThreads = 1
            }));
}
