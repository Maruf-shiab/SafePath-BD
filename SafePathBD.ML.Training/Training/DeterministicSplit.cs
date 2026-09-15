using SafePathBD.ML.Training.Models;

namespace SafePathBD.ML.Training.Training;

public static class DeterministicSplit
{
    public static DatasetSplit Primary(IReadOnlyList<TrafficTrainingRow> rows)
    {
        var train = new List<TrafficTrainingRow>();
        var validation = new List<TrafficTrainingRow>();
        var test = new List<TrafficTrainingRow>();
        foreach (var row in rows)
        {
            var bucket = StableBucket($"{row.RoadId}|{row.DayIndex}|{row.Hour}", 100);
            if (bucket < 70) train.Add(row);
            else if (bucket < 85) validation.Add(row);
            else test.Add(row);
        }
        return new DatasetSplit(train, validation, test);
    }

    public static (IReadOnlyList<TrafficTrainingRow> Train, IReadOnlyList<TrafficTrainingRow> Test) RoadHoldout(IReadOnlyList<TrafficTrainingRow> rows)
    {
        var holdoutRoads = rows.Select(r => r.RoadId).Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(id => StableBucket(id, 5) == 0)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        return (rows.Where(r => !holdoutRoads.Contains(r.RoadId)).ToList(), rows.Where(r => holdoutRoads.Contains(r.RoadId)).ToList());
    }

    public static int StableBucket(string value, int modulus)
    {
        unchecked
        {
            uint hash = 2166136261;
            foreach (var ch in value) { hash ^= ch; hash *= 16777619; }
            return (int)(hash % (uint)modulus);
        }
    }
}
