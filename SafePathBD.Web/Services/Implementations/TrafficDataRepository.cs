using System.Globalization;
using Microsoft.Extensions.Options;
using SafePathBD.Web.Common;
using SafePathBD.Web.Models.DTOs.Traffic;
using SafePathBD.Web.Models.ML;
using SafePathBD.Web.Services.Interfaces;

namespace SafePathBD.Web.Services.Implementations;

public sealed class TrafficDataRepository : ITrafficDataRepository
{
    private static readonly string[] Modes = ["CAR", "MOTORBIKE", "RICKSHAW", "BUS", "WALK"];

    private readonly IReadOnlyDictionary<string, TrafficRoadProfile> _roads;
    private readonly IReadOnlyDictionary<string, List<TrafficPattern>> _patternsByRoad;
    private readonly IReadOnlyDictionary<(string RoadId, string DayType, int Hour), double> _exactMeans;
    private readonly IReadOnlyDictionary<(string RoadId, int Hour), double> _roadHourMeans;
    private readonly IReadOnlyDictionary<(string RoadClass, int Hour), double> _classHourMeans;
    private readonly IReadOnlyDictionary<int, double> _globalHourMeans;
    private readonly IReadOnlyDictionary<(string RoadClass, int Bin, string Mode), double> _speedCalibration;
    private readonly IReadOnlyDictionary<(string Mode, int Bin), double> _globalSpeedCalibration;
    private readonly IReadOnlyDictionary<string, ulong?> _roadSegmentMapping;
    private readonly double _maxMatchMeters;

    public int RowCount { get; }
    public int RoadCount => _roads.Count;
    public string DataStatus { get; }

    public TrafficDataRepository(
        IWebHostEnvironment environment,
        IOptions<IntelligentMobilityOptions> options,
        ILogger<TrafficDataRepository> logger)
    {
        var config = options.Value;
        _maxMatchMeters = config.TrafficRoadMatchMaxMeters;
        var datasetPath = Resolve(environment.ContentRootPath, config.TrafficDatasetPath);
        var mappingPath = Resolve(environment.ContentRootPath, config.TrafficRoadMappingPath);

        if (!File.Exists(datasetPath))
        {
            throw new FileNotFoundException("SafePath traffic development dataset was not found.", datasetPath);
        }

        var rows = LoadPatterns(datasetPath, out var dataStatus);
        RowCount = rows.Count;
        DataStatus = dataStatus;
        _patternsByRoad = rows.GroupBy(r => r.RoadId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        _roads = rows.GroupBy(r => r.RoadId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g =>
                {
                    var first = g.First();
                    return new TrafficRoadProfile(
                        first.RoadId,
                        first.RoadName,
                        first.SegmentName,
                        first.Area,
                        first.RoadClass,
                        first.SegmentLengthKm,
                        first.CenterLatitude,
                        first.CenterLongitude,
                        first.DistanceFromAustKm,
                        (float)g.Average(x => x.RoadConditionScore));
                },
                StringComparer.OrdinalIgnoreCase);

        _exactMeans = rows.GroupBy(r => (r.RoadId.ToUpperInvariant(), r.DayType.ToUpperInvariant(), r.Hour))
            .ToDictionary(g => g.Key, g => g.Average(x => x.Congestion));
        _roadHourMeans = rows.GroupBy(r => (r.RoadId.ToUpperInvariant(), r.Hour))
            .ToDictionary(g => g.Key, g => g.Average(x => x.Congestion));
        _classHourMeans = rows.GroupBy(r => (r.RoadClass.ToUpperInvariant(), r.Hour))
            .ToDictionary(g => g.Key, g => g.Average(x => x.Congestion));
        _globalHourMeans = rows.GroupBy(r => r.Hour).ToDictionary(g => g.Key, g => g.Average(x => x.Congestion));

        var speedByClass = new Dictionary<(string RoadClass, int Bin, string Mode), double>();
        var speedGlobal = new Dictionary<(string Mode, int Bin), double>();
        foreach (var mode in Modes)
        {
            foreach (var group in rows.GroupBy(r => (r.RoadClass.ToUpperInvariant(), Bin(r.Congestion))))
            {
                var speeds = group.Select(r => r.SpeedFor(mode)).Where(v => v > 0).ToArray();
                if (speeds.Length > 0)
                {
                    speedByClass[(group.Key.Item1, group.Key.Item2, mode)] = speeds.Average();
                }
            }

            foreach (var group in rows.GroupBy(r => Bin(r.Congestion)))
            {
                var speeds = group.Select(r => r.SpeedFor(mode)).Where(v => v > 0).ToArray();
                if (speeds.Length > 0)
                {
                    speedGlobal[(mode, group.Key)] = speeds.Average();
                }
            }
        }
        _speedCalibration = speedByClass;
        _globalSpeedCalibration = speedGlobal;
        _roadSegmentMapping = LoadMapping(mappingPath);

        logger.LogInformation(
            "Loaded SafePath synthetic traffic patterns: rows={Rows}, roads={Roads}, status={Status}.",
            RowCount,
            RoadCount,
            DataStatus);
    }

    public TrafficRoadMatch? FindNearestRoad(double latitude, double longitude)
    {
        TrafficRoadProfile? best = null;
        var bestMeters = double.PositiveInfinity;
        foreach (var road in _roads.Values)
        {
            var meters = GeoMath.HaversineDistanceKm(latitude, longitude, road.CenterLatitude, road.CenterLongitude) * 1000d;
            if (meters < bestMeters)
            {
                bestMeters = meters;
                best = road;
            }
        }

        if (best is null || bestMeters > _maxMatchMeters)
        {
            return null;
        }

        var confidence = bestMeters <= 250 ? DataConfidenceLevels.High
            : bestMeters <= 650 ? DataConfidenceLevels.Medium
            : DataConfidenceLevels.Low;

        return new TrafficRoadMatch(
            best.RoadId,
            best.RoadName,
            best.RoadClass,
            best.Area,
            Math.Round(bestMeters, 1),
            confidence,
            best.RoadConditionScore,
            best.DistanceFromAustKm);
    }

    public TrafficRoadProfile? GetRoad(string roadId) =>
        _roads.TryGetValue(roadId, out var road) ? road : null;

    public TrafficFallbackResult GetFallback(string roadId, DateTimeOffset at, string roadClass)
    {
        var dayType = TrafficFeatureEngineering.DayType(at.DayOfWeek).ToUpperInvariant();
        var id = roadId.ToUpperInvariant();
        var roadClassKey = (roadClass ?? string.Empty).ToUpperInvariant();

        if (_exactMeans.TryGetValue((id, dayType, at.Hour), out var exact))
        {
            return new TrafficFallbackResult(exact, TrafficPredictionSources.ExactRoadContextFallback, DataConfidenceLevels.Medium);
        }
        if (_roadHourMeans.TryGetValue((id, at.Hour), out var roadHour))
        {
            return new TrafficFallbackResult(roadHour, TrafficPredictionSources.RoadHourFallback, DataConfidenceLevels.Medium);
        }
        if (_classHourMeans.TryGetValue((roadClassKey, at.Hour), out var classHour))
        {
            return new TrafficFallbackResult(classHour, TrafficPredictionSources.RoadClassHourFallback, DataConfidenceLevels.Low);
        }

        var global = _globalHourMeans.TryGetValue(at.Hour, out var globalHour)
            ? globalHour
            : _globalHourMeans.Values.DefaultIfEmpty(50d).Average();
        return new TrafficFallbackResult(global, TrafficPredictionSources.GlobalHourFallback, DataConfidenceLevels.Low);
    }

    public double GetCalibratedSpeedKph(string mode, string roadClass, double predictedCongestionIndex)
    {
        var normalizedMode = mode.Trim().ToUpperInvariant();
        var bin = Bin(predictedCongestionIndex);
        var roadClassKey = (roadClass ?? string.Empty).Trim().ToUpperInvariant();

        if (_speedCalibration.TryGetValue((roadClassKey, bin, normalizedMode), out var speed))
        {
            return ClampSpeed(normalizedMode, speed);
        }
        if (_globalSpeedCalibration.TryGetValue((normalizedMode, bin), out speed))
        {
            return ClampSpeed(normalizedMode, speed);
        }

        return normalizedMode switch
        {
            MobilityModes.Walk => 4.8,
            MobilityModes.Rickshaw => 9.5,
            MobilityModes.Motorbike => 22,
            MobilityModes.Bus => 13,
            MobilityModes.Car => 18,
            _ => 10
        };
    }

    public BusCalibration GetBusCalibration(string roadId, DateTimeOffset at)
    {
        if (!_patternsByRoad.TryGetValue(roadId, out var rows))
        {
            return new BusCalibration(false, 0);
        }

        var matching = rows.Where(r => r.Hour == at.Hour).ToArray();
        if (matching.Length == 0)
        {
            matching = rows.ToArray();
        }

        var available = matching.Any(r => r.BusAvailable);
        var waits = matching.Where(r => r.ExpectedBusWaitMinutes.HasValue)
            .Select(r => r.ExpectedBusWaitMinutes!.Value)
            .ToArray();
        return new BusCalibration(available, waits.Length > 0 ? waits.Average() : 0);
    }

    public double GetTrafficVolatility(string roadId, DateTimeOffset at)
    {
        if (!_patternsByRoad.TryGetValue(roadId, out var rows))
        {
            return 50;
        }

        var dayType = TrafficFeatureEngineering.DayType(at.DayOfWeek);
        var values = rows.Where(r => string.Equals(r.DayType, dayType, StringComparison.OrdinalIgnoreCase))
            .Select(r => r.Congestion)
            .ToArray();
        if (values.Length < 2)
        {
            values = rows.Select(r => r.Congestion).ToArray();
        }
        if (values.Length < 2)
        {
            return 0;
        }

        var mean = values.Average();
        var variance = values.Average(v => Math.Pow(v - mean, 2));
        return Math.Clamp(Math.Sqrt(variance) / 30d * 100d, 0, 100);
    }

    public ulong? GetMappedRoadSegmentId(string roadId) =>
        _roadSegmentMapping.TryGetValue(roadId, out var segmentId) ? segmentId : null;

    private static string Resolve(string root, string relativeOrAbsolute) =>
        Path.IsPathRooted(relativeOrAbsolute) ? relativeOrAbsolute : Path.Combine(root, relativeOrAbsolute.Replace('/', Path.DirectorySeparatorChar));

    private static int Bin(double congestion) => Math.Clamp((int)Math.Floor(Math.Clamp(congestion, 0, 100) / 10d), 0, 9);

    private static double ClampSpeed(string mode, double speed) => mode switch
    {
        MobilityModes.Walk => Math.Clamp(speed, 3.2, 6.5),
        MobilityModes.Rickshaw => Math.Clamp(speed, 5, 18),
        MobilityModes.Motorbike => Math.Clamp(speed, 8, 60),
        MobilityModes.Bus => Math.Clamp(speed, 5, 45),
        MobilityModes.Car => Math.Clamp(speed, 5, 55),
        _ => Math.Clamp(speed, 3, 60)
    };

    private static IReadOnlyDictionary<string, ulong?> LoadMapping(string path)
    {
        if (!File.Exists(path))
        {
            return new Dictionary<string, ulong?>(StringComparer.OrdinalIgnoreCase);
        }

        var rows = SimpleCsv.ReadRows(path).ToList();
        if (rows.Count <= 1)
        {
            return new Dictionary<string, ulong?>(StringComparer.OrdinalIgnoreCase);
        }

        var result = new Dictionary<string, ulong?>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows.Skip(1))
        {
            if (row.Length == 0 || string.IsNullOrWhiteSpace(row[0]))
            {
                continue;
            }

            ulong? segmentId = null;
            if (row.Length > 1 && ulong.TryParse(row[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            {
                segmentId = parsed;
            }
            result[row[0].Trim()] = segmentId;
        }
        return result;
    }

    private static List<TrafficPattern> LoadPatterns(string path, out string dataStatus)
    {
        var rows = SimpleCsv.ReadRows(path).ToList();
        if (rows.Count < 2)
        {
            throw new InvalidDataException("Traffic dataset is empty.");
        }

        var header = rows[0].Select((name, index) => (name: name.Trim(), index))
            .ToDictionary(x => x.name, x => x.index, StringComparer.OrdinalIgnoreCase);
        string Get(string[] row, string name) => header.TryGetValue(name, out var index) && index < row.Length ? row[index] : string.Empty;
        double D(string[] row, string name) => double.TryParse(Get(row, name), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : 0;
        int I(string[] row, string name) => int.TryParse(Get(row, name), NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : 0;
        double? N(string[] row, string name) => double.TryParse(Get(row, name), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : null;

        var result = new List<TrafficPattern>(rows.Count - 1);
        foreach (var row in rows.Skip(1))
        {
            var roadId = Get(row, "road_id");
            if (string.IsNullOrWhiteSpace(roadId))
            {
                continue;
            }

            result.Add(new TrafficPattern(
                roadId,
                Get(row, "road_name"),
                Get(row, "segment_name"),
                Get(row, "area"),
                Get(row, "road_class"),
                D(row, "segment_length_km"),
                D(row, "center_lat"),
                D(row, "center_lon"),
                D(row, "distance_from_aust_km"),
                Get(row, "day_of_week"),
                Get(row, "day_type"),
                I(row, "hour"),
                Get(row, "weather_condition"),
                D(row, "congestion_index_0_100"),
                D(row, "avg_speed_car_kph"),
                D(row, "avg_speed_motorbike_kph"),
                D(row, "avg_speed_rickshaw_kph"),
                D(row, "avg_speed_bus_kph"),
                D(row, "avg_speed_walk_kph"),
                I(row, "bus_available") == 1,
                N(row, "expected_bus_wait_min"),
                D(row, "road_condition_score_0_100"),
                Get(row, "data_status")));
        }

        dataStatus = result.Select(r => r.DataStatus).FirstOrDefault(s => !string.IsNullOrWhiteSpace(s)) ?? "SYNTHETIC_DEVELOPMENT";
        return result;
    }

    private sealed record TrafficPattern(
        string RoadId,
        string RoadName,
        string SegmentName,
        string Area,
        string RoadClass,
        double SegmentLengthKm,
        double CenterLatitude,
        double CenterLongitude,
        double DistanceFromAustKm,
        string DayOfWeek,
        string DayType,
        int Hour,
        string Weather,
        double Congestion,
        double CarSpeed,
        double MotorbikeSpeed,
        double RickshawSpeed,
        double BusSpeed,
        double WalkSpeed,
        bool BusAvailable,
        double? ExpectedBusWaitMinutes,
        double RoadConditionScore,
        string DataStatus)
    {
        public double SpeedFor(string mode) => mode switch
        {
            MobilityModes.Car => CarSpeed,
            MobilityModes.Motorbike => MotorbikeSpeed,
            MobilityModes.Rickshaw => RickshawSpeed,
            MobilityModes.Bus => BusSpeed,
            MobilityModes.Walk => WalkSpeed,
            _ => 0
        };
    }
}
