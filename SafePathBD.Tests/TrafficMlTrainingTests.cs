using Microsoft.ML;
using SafePathBD.ML.Training.Models;
using SafePathBD.ML.Training.Training;

namespace SafePathBD.Tests;

public sealed class TrafficMlTrainingTests
{
    [Fact]
    public void FeatureContract_ExcludesTargetDerivedLeakageFields()
    {
        var forbidden = new HashSet<string>(TrafficFeatureBuilder.ExcludedLeakageFeatures, StringComparer.OrdinalIgnoreCase);
        foreach (var feature in TrafficFeatureBuilder.FeatureGroups)
            Assert.DoesNotContain(feature, forbidden);

        Assert.Contains("avg_speed_car_kph", forbidden);
        Assert.Contains("traffic_level", forbidden);
        Assert.Contains("travel_min_per_km_bus", forbidden);
    }

    [Fact]
    public void ProductionPredictionPolicy_ClampsToZeroHundred()
    {
        Assert.Equal(0d, SafePathBD.Web.Common.TrafficLevels.ClampIndex(-12));
        Assert.Equal(100d, SafePathBD.Web.Common.TrafficLevels.ClampIndex(140));
        Assert.Equal(67.5d, SafePathBD.Web.Common.TrafficLevels.ClampIndex(67.5));
    }

    [Fact]
    public void DeterministicSplit_IsStableAndShared()
    {
        var rows = CreateRows(1200);
        var first = DeterministicSplit.Primary(rows);
        var second = DeterministicSplit.Primary(rows);
        Assert.Equal(first.Train.Select(x => x.RecordId), second.Train.Select(x => x.RecordId));
        Assert.Equal(first.Validation.Select(x => x.RecordId), second.Validation.Select(x => x.RecordId));
        Assert.Equal(first.Test.Select(x => x.RecordId), second.Test.Select(x => x.RecordId));
        Assert.NotEmpty(first.Train); Assert.NotEmpty(first.Validation); Assert.NotEmpty(first.Test);
    }

    [Fact]
    public void ModelSelector_UsesMaeThenRmseThenR2()
    {
        var results = new[]
        {
            Result("FASTTREE", 5.00, 7.0, .80),
            Result("LIGHTGBM", 5.005, 6.8, .79),
            Result("SDCA", 5.005, 6.8, .82)
        };
        var selected = ModelSelector.Select(results);
        Assert.Equal("SDCA", selected.ModelName);
    }

    [Fact]
    public void AllThreeRequiredModelPipelines_CanTrainOnSamePartition()
    {
        var ml = new MLContext(seed: TrafficFeatureBuilder.Seed);
        var rows = CreateRows(600);
        var split = DeterministicSplit.Primary(rows);
        var data = ml.Data.LoadFromEnumerable(split.Train);
        Assert.NotNull(ModelTrainers.FastTree(ml).Fit(data));
        Assert.NotNull(ModelTrainers.LightGbm(ml).Fit(data));
        Assert.NotNull(ModelTrainers.Sdca(ml).Fit(data));
    }


    [Fact]
    public void TreeFamilies_HaveSmallDeterministicTuningSets()
    {
        var ml = new MLContext(seed: TrafficFeatureBuilder.Seed);
        Assert.Equal(3, ModelTrainers.FastTreeCandidates(ml).Count);
        Assert.Equal(3, ModelTrainers.LightGbmCandidates(ml).Count);
        Assert.Single(ModelTrainers.SdcaCandidates(ml));
        Assert.All(ModelTrainers.FastTreeCandidates(ml), c => Assert.Equal("FASTTREE", c.Family));
        Assert.All(ModelTrainers.LightGbmCandidates(ml), c => Assert.Equal("LIGHTGBM", c.Family));
    }

    [Fact]
    public void ModelSelector_RejectsExcessiveClampRate()
    {
        var unstable = Result("LIGHTGBM", 1.0, 1.2, .95);
        unstable.ClampRatePercent = 35;
        var stable = Result("FASTTREE", 2.0, 2.2, .80);
        Assert.Equal("FASTTREE", ModelSelector.Select([unstable, stable]).ModelName);
    }

    [Fact]
    public void ProductionArtifact_IsPublishedFromSelectedModelArtifact()
    {
        var dir = Path.Combine(Path.GetTempPath(), "safepath-ml-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var source = Path.Combine(dir, "winner.zip");
            var canonical = Path.Combine(dir, "traffic-congestion-model.zip");
            File.WriteAllText(source, "selected-model-bytes");
            var selected = Result("LIGHTGBM", 1, 1, .9);
            selected.ArtifactPath = source;
            ModelArtifactPublisher.PublishSelected(selected, canonical);
            Assert.Equal(File.ReadAllText(source), File.ReadAllText(canonical));
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void Baseline_IsCalculatedFromTrainingOnly()
    {
        var rows = CreateRows(1000);
        var split = DeterministicSplit.Primary(rows);
        var metrics = BaselineEvaluator.Evaluate(split.Train, split.Validation);
        Assert.True(double.IsFinite(metrics.Mae));
        Assert.True(metrics.Mae >= 0);
        Assert.True(double.IsFinite(metrics.Rmse));
    }

    private static ModelEvaluationResult Result(string name, double mae, double rmse, double r2) => new()
    {
        ModelName = name,
        Validation = new RegressionMetricsDto(mae, rmse, r2),
        ValidationClasses = new ClassMetricsDto(.8,.8,.8,.8)
    };

    private static List<TrafficTrainingRow> CreateRows(int count)
    {
        var roads = Enumerable.Range(1, 20).Select(i => $"R{i:000}").ToArray();
        var result = new List<TrafficTrainingRow>(count);
        for (var i = 0; i < count; i++)
        {
            var hour = i % 24;
            var day = (i / 24) % 7;
            var row = new TrafficTrainingRow
            {
                RecordId = i.ToString(), RoadId = roads[i % roads.Length], RoadClass = i % 3 == 0 ? "primary" : "secondary",
                Area = "A" + (i % 4), DayType = day is 4 or 5 ? "Weekend" : "Weekday", WeatherCondition = i % 5 == 0 ? "Rain" : "Clear",
                DayIndex = day, Hour = hour, RoadConditionScore = 70 + i % 20, DistanceFromAustKm = i % 10,
                CongestionIndex = (float)Math.Clamp(15 + hour * 2.4 + (i % 7), 0, 100)
            };
            result.Add(TrafficFeatureBuilder.WithTimeFeatures(row));
        }
        return result;
    }
}
