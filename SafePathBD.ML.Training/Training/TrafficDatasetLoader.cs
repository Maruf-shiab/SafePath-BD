using System.Globalization;
using System.Text;
using SafePathBD.ML.Training.Models;

namespace SafePathBD.ML.Training.Training;

public static class TrafficDatasetLoader
{
    public static IReadOnlyList<TrafficTrainingRow> Load(string path)
    {
        var lines = File.ReadLines(path).GetEnumerator();
        if (!lines.MoveNext()) return Array.Empty<TrafficTrainingRow>();
        var header = ParseCsv(lines.Current).Select((name, i) => (name, i))
            .ToDictionary(x => x.name, x => x.i, StringComparer.OrdinalIgnoreCase);
        var result = new List<TrafficTrainingRow>();
        while (lines.MoveNext())
        {
            var cells = ParseCsv(lines.Current);
            if (cells.Count < header.Count) continue;
            var day = Get(cells, header, "day_of_week");
            var row = new TrafficTrainingRow
            {
                RecordId = Get(cells, header, "record_id"),
                RoadId = Get(cells, header, "road_id"),
                RoadClass = Get(cells, header, "road_class"),
                Area = Get(cells, header, "area"),
                DayType = Get(cells, header, "day_type"),
                WeatherCondition = Get(cells, header, "weather_condition"),
                DayIndex = DayIndex(day),
                Hour = Int(Get(cells, header, "hour")),
                RoadConditionScore = Float(Get(cells, header, "road_condition_score_0_100")),
                DistanceFromAustKm = Float(Get(cells, header, "distance_from_aust_km")),
                CongestionIndex = Float(Get(cells, header, "congestion_index_0_100"))
            };
            result.Add(TrafficFeatureBuilder.WithTimeFeatures(row));
        }
        return result;
    }

    private static int DayIndex(string value) => value.Trim().ToUpperInvariant() switch
    {
        "MONDAY" => 0, "TUESDAY" => 1, "WEDNESDAY" => 2, "THURSDAY" => 3,
        "FRIDAY" => 4, "SATURDAY" => 5, "SUNDAY" => 6, _ => 0
    };
    private static string Get(IReadOnlyList<string> cells, IReadOnlyDictionary<string,int> h, string key) => h.TryGetValue(key, out var i) && i < cells.Count ? cells[i].Trim() : string.Empty;
    private static int Int(string s) => int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : 0;
    private static float Float(string s) => float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : 0;

    private static List<string> ParseCsv(string line)
    {
        var result = new List<string>();
        var b = new StringBuilder();
        var quoted = false;
        for (var i = 0; i < line.Length; i++)
        {
            var ch = line[i];
            if (ch == '"')
            {
                if (quoted && i + 1 < line.Length && line[i + 1] == '"') { b.Append('"'); i++; }
                else quoted = !quoted;
            }
            else if (ch == ',' && !quoted) { result.Add(b.ToString()); b.Clear(); }
            else b.Append(ch);
        }
        result.Add(b.ToString());
        return result;
    }
}
