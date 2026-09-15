using Microsoft.ML;
using SafePathBD.ML.Training.Models;

namespace SafePathBD.ML.Training.Training;

public static class PermutationFeatureImportance
{
    public static IReadOnlyDictionary<string, double> Calculate(MLContext ml, ITransformer model, IReadOnlyList<TrafficTrainingRow> validation, int seed)
    {
        var evaluator = new ModelEvaluator(ml);
        var baseline = evaluator.Evaluate(model, validation).Regression.Mae;
        var result = new Dictionary<string, double>();
        foreach (var feature in TrafficFeatureBuilder.FeatureGroups)
        {
            var clone = validation.Select(Clone).ToList();
            var rng = new Random(seed + DeterministicSplit.StableBucket(feature, 10000));
            var shuffled = clone.OrderBy(_ => rng.Next()).Select(Get(feature)).ToArray();
            for (var i = 0; i < clone.Count; i++) Set(clone[i], feature, shuffled[i]);
            var mae = evaluator.Evaluate(model, clone).Regression.Mae;
            result[feature] = Math.Max(0, mae - baseline);
        }
        return result.OrderByDescending(x => x.Value).ToDictionary(x => x.Key, x => x.Value);
    }

    private static TrafficTrainingRow Clone(TrafficTrainingRow r) => new()
    {
        RecordId=r.RecordId,RoadId=r.RoadId,RoadClass=r.RoadClass,Area=r.Area,DayType=r.DayType,WeatherCondition=r.WeatherCondition,
        DayIndex=r.DayIndex,Hour=r.Hour,HourSin=r.HourSin,HourCos=r.HourCos,DaySin=r.DaySin,DayCos=r.DayCos,
        RoadConditionScore=r.RoadConditionScore,DistanceFromAustKm=r.DistanceFromAustKm,CongestionIndex=r.CongestionIndex
    };
    private static object GetValue(string f, TrafficTrainingRow r) => f switch
    {
        "RoadId"=>r.RoadId,"RoadClass"=>r.RoadClass,"Area"=>r.Area,"DayType"=>r.DayType,"WeatherCondition"=>r.WeatherCondition,
        "HourSin"=>r.HourSin,"HourCos"=>r.HourCos,"DaySin"=>r.DaySin,"DayCos"=>r.DayCos,
        "RoadConditionScore"=>r.RoadConditionScore,"DistanceFromAustKm"=>r.DistanceFromAustKm,_=>string.Empty
    };
    private static Func<TrafficTrainingRow, object> Get(string f) => r => GetValue(f, r);
    private static void Set(TrafficTrainingRow r, string f, object value)
    {
        switch (f)
        {
            case "RoadId": r.RoadId=(string)value; break; case "RoadClass": r.RoadClass=(string)value; break; case "Area": r.Area=(string)value; break;
            case "DayType": r.DayType=(string)value; break; case "WeatherCondition": r.WeatherCondition=(string)value; break;
            case "HourSin": r.HourSin=Convert.ToSingle(value); break; case "HourCos": r.HourCos=Convert.ToSingle(value); break;
            case "DaySin": r.DaySin=Convert.ToSingle(value); break; case "DayCos": r.DayCos=Convert.ToSingle(value); break;
            case "RoadConditionScore": r.RoadConditionScore=Convert.ToSingle(value); break; case "DistanceFromAustKm": r.DistanceFromAustKm=Convert.ToSingle(value); break;
        }
    }
}
