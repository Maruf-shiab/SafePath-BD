using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Microsoft.ML;
using SafePathBD.Web.Common;
using SafePathBD.Web.Models.DTOs.Traffic;
using SafePathBD.Web.Models.ML;
using SafePathBD.Web.Services.Interfaces;

namespace SafePathBD.Web.Services.Implementations;

public sealed class TrafficPredictionService : ITrafficPredictionService
{
    private readonly ITrafficDataRepository _data;
    private readonly IMemoryCache _cache;
    private readonly IntelligentMobilityOptions _options;
    private readonly ILogger<TrafficPredictionService> _logger;
    private readonly object _predictionLock = new();
    private readonly PredictionEngine<TrafficModelInput, TrafficModelPrediction>? _engine;

    public bool ModelAvailable => _engine is not null;
    public string ModelName { get; }
    public string ModelVersion { get; }

    public TrafficPredictionService(
        ITrafficDataRepository data,
        IMemoryCache cache,
        IWebHostEnvironment environment,
        IOptions<IntelligentMobilityOptions> options,
        ILogger<TrafficPredictionService> logger)
    {
        _data = data;
        _cache = cache;
        _options = options.Value;
        _logger = logger;

        ModelName = "Traffic fallback hierarchy";
        ModelVersion = "fallback-1.0";

        var modelPath = Resolve(environment.ContentRootPath, _options.TrafficModelPath);
        var metadataPath = Resolve(environment.ContentRootPath, _options.TrafficModelMetadataPath);
        if (!File.Exists(modelPath))
        {
            logger.LogWarning(
                "Traffic ML artifact was not found at {Path}. SafePath will use the documented synthetic-pattern fallback hierarchy until training is run.",
                modelPath);
            return;
        }

        try
        {
            var ml = new MLContext(seed: 24091);
            var model = ml.Model.Load(modelPath, out _);
            _engine = ml.Model.CreatePredictionEngine<TrafficModelInput, TrafficModelPrediction>(model);

            if (File.Exists(metadataPath))
            {
                using var stream = File.OpenRead(metadataPath);
                var metadata = JsonSerializer.Deserialize<TrafficModelMetadata>(stream, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                ModelName = string.IsNullOrWhiteSpace(metadata?.SelectedModel) ? "ML.NET model" : metadata!.SelectedModel!;
                ModelVersion = string.IsNullOrWhiteSpace(metadata?.ModelVersion) ? "1.0" : metadata!.ModelVersion!;
            }
            else
            {
                ModelName = "ML.NET selected model";
                ModelVersion = "1.0";
            }

            logger.LogInformation("Loaded SafePath traffic ML model {Model} v{Version}.", ModelName, ModelVersion);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Traffic model could not be loaded. Falling back to synthetic traffic pattern means.");
            _engine = null;
            ModelName = "Traffic fallback hierarchy";
            ModelVersion = "fallback-1.0";
        }
    }

    public Task<TrafficPredictionResult> PredictAsync(
        TrafficPredictionRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var roadMatch = ResolveRoad(request);
        var road = roadMatch?.RoadId is { Length: > 0 } id ? _data.GetRoad(id) : null;
        var roadId = road?.RoadId ?? request.TrafficRoadId ?? string.Empty;
        var roadClass = road?.RoadClass ?? request.RoadClass ?? "unknown";
        var area = road?.Area ?? request.Area ?? "unknown";
        var matchDistance = request.MatchDistanceMeters ?? roadMatch?.DistanceMeters;
        var weather = string.IsNullOrWhiteSpace(request.WeatherCondition)
            ? _options.DefaultWeatherCondition
            : request.WeatherCondition.Trim();
        var roadCondition = request.RoadConditionScore ?? road?.RoadConditionScore ?? roadMatch?.RoadConditionScore ?? 70f;
        var distanceFromAust = request.DistanceFromAustKm ?? road?.DistanceFromAustKm ?? roadMatch?.DistanceFromAustKm ?? 0d;

        var cacheKey = string.Create(
            System.Globalization.CultureInfo.InvariantCulture,
            $"traffic:{roadId}:{request.At:yyyyMMddHH}:{weather}:{Math.Round(roadCondition)}");
        if (_cache.TryGetValue<TrafficPredictionResult>(cacheKey, out var cached))
        {
            return Task.FromResult(cached!);
        }

        TrafficPredictionResult result;
        if (_engine is not null && road is not null)
        {
            var hour = TrafficFeatureEngineering.Cyclic(request.At.Hour, 24);
            var dayIndex = TrafficFeatureEngineering.DayIndex(request.At.DayOfWeek);
            var day = TrafficFeatureEngineering.Cyclic(dayIndex, 7);
            var input = new TrafficModelInput
            {
                RoadId = road.RoadId,
                RoadClass = roadClass,
                Area = area,
                DayType = TrafficFeatureEngineering.DayType(request.At.DayOfWeek),
                WeatherCondition = weather,
                HourSin = hour.Sin,
                HourCos = hour.Cos,
                DaySin = day.Sin,
                DayCos = day.Cos,
                RoadConditionScore = roadCondition,
                DistanceFromAustKm = (float)distanceFromAust
            };

            float raw;
            lock (_predictionLock)
            {
                raw = _engine.Predict(input).Score;
            }

            if (!float.IsFinite(raw))
            {
                result = Fallback(roadId, roadClass, request.At, road, matchDistance);
            }
            else
            {
                var score = (float)TrafficLevels.ClampIndex(raw);
                var confidence = matchDistance switch
                {
                    <= 350 => DataConfidenceLevels.High,
                    <= 750 => DataConfidenceLevels.Medium,
                    _ => roadMatch is null ? DataConfidenceLevels.Medium : DataConfidenceLevels.Low
                };
                result = new TrafficPredictionResult(
                    Math.Round(score, 1),
                    TrafficLevels.FromIndex(score),
                    TrafficPredictionSources.MlModel,
                    confidence,
                    ModelName,
                    ModelVersion,
                    road.RoadId,
                    road.RoadName,
                    road.RoadClass,
                    matchDistance is null ? null : Math.Round(matchDistance.Value, 1));
            }
        }
        else
        {
            result = Fallback(roadId, roadClass, request.At, road, matchDistance);
        }

        _cache.Set(cacheKey, result, TimeSpan.FromMinutes(_options.TrafficPredictionCacheMinutes));
        return Task.FromResult(result);
    }

    private TrafficPredictionResult Fallback(
        string roadId,
        string roadClass,
        DateTimeOffset at,
        TrafficRoadProfile? road,
        double? matchDistance)
    {
        var fallback = _data.GetFallback(roadId, at, roadClass);
        return new TrafficPredictionResult(
            Math.Round(TrafficPredictionPolicy.ClampIndex(fallback.CongestionIndex), 1),
            TrafficLevels.FromIndex(fallback.CongestionIndex),
            fallback.Source,
            fallback.Confidence,
            "Synthetic traffic pattern fallback",
            "fallback-1.0",
            string.IsNullOrWhiteSpace(roadId) ? null : roadId,
            road?.RoadName,
            road?.RoadClass ?? roadClass,
            matchDistance is null ? null : Math.Round(matchDistance.Value, 1));
    }

    private TrafficRoadMatch? ResolveRoad(TrafficPredictionRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.TrafficRoadId))
        {
            var road = _data.GetRoad(request.TrafficRoadId);
            if (road is not null)
            {
                return new TrafficRoadMatch(
                    road.RoadId,
                    road.RoadName,
                    road.RoadClass,
                    road.Area,
                    request.MatchDistanceMeters ?? 0,
                    DataConfidenceLevels.High,
                    road.RoadConditionScore,
                    road.DistanceFromAustKm);
            }
        }

        return _data.FindNearestRoad(request.Latitude, request.Longitude);
    }

    private static string Resolve(string root, string relativeOrAbsolute) =>
        Path.IsPathRooted(relativeOrAbsolute) ? relativeOrAbsolute : Path.Combine(root, relativeOrAbsolute.Replace('/', Path.DirectorySeparatorChar));

    private sealed class TrafficModelMetadata
    {
        public string? SelectedModel { get; set; }
        public string? ModelVersion { get; set; }
    }
}
