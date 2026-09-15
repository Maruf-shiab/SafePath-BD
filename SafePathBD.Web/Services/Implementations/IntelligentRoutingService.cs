using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using SafePathBD.Web.Common;
using SafePathBD.Web.Models.DTOs.IntelligentRouting;
using SafePathBD.Web.Models.DTOs.Routing;
using SafePathBD.Web.Services.IntelligentRouting;
using SafePathBD.Web.Services.Interfaces;

namespace SafePathBD.Web.Services.Implementations;

public sealed class IntelligentRoutingService : IIntelligentRoutingService
{
    private const double MinimumEndpointSeparationMeters = 25d;
    private readonly IRoutingService _routing;
    private readonly IMultimodalGraphBuilder _graphBuilder;
    private readonly IKBestJourneyPlanner _planner;
    private readonly IJourneyAssembler _assembler;
    private readonly IRouteExplanationService _explanations;
    private readonly IDepartureWindowService _departureWindow;
    private readonly ITrafficPredictionService _traffic;
    private readonly ITrafficDataRepository _trafficData;
    private readonly IMemoryCache _cache;
    private readonly IntelligentMobilityOptions _options;
    private readonly ILogger<IntelligentRoutingService> _logger;

    public IntelligentRoutingService(
        IRoutingService routing,
        IMultimodalGraphBuilder graphBuilder,
        IKBestJourneyPlanner planner,
        IJourneyAssembler assembler,
        IRouteExplanationService explanations,
        IDepartureWindowService departureWindow,
        ITrafficPredictionService traffic,
        ITrafficDataRepository trafficData,
        IMemoryCache cache,
        IOptions<IntelligentMobilityOptions> options,
        ILogger<IntelligentRoutingService> logger)
    {
        _routing = routing;
        _graphBuilder = graphBuilder;
        _planner = planner;
        _assembler = assembler;
        _explanations = explanations;
        _departureWindow = departureWindow;
        _traffic = traffic;
        _trafficData = trafficData;
        _cache = cache;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<IntelligentRouteSearchResult> SearchAsync(
        IntelligentRouteRequest request,
        ulong? userId,
        CancellationToken cancellationToken = default)
    {
        var validation = ValidateAndNormalize(request);
        if (validation is not null)
        {
            return new IntelligentRouteSearchResult(IntelligentRouteStatuses.Invalid, validation, null);
        }

        var baseRoutes = await _routing.SearchAsync(
            new RouteSearchRequest(request.Start, request.Destination),
            userId,
            cancellationToken);
        if (baseRoutes.Status != RouteSearchStatus.Success || baseRoutes.Response is null)
        {
            var status = baseRoutes.Status switch
            {
                RouteSearchStatus.InvalidRequest => IntelligentRouteStatuses.Invalid,
                RouteSearchStatus.NoRoute => IntelligentRouteStatuses.NoRoute,
                RouteSearchStatus.ProviderUnavailable => IntelligentRouteStatuses.ProviderUnavailable,
                _ => IntelligentRouteStatuses.ProviderUnavailable
            };
            return new IntelligentRouteSearchResult(status, baseRoutes.Message, null);
        }

        var graph = _graphBuilder.Build(baseRoutes.Response.Candidates);
        var paths = await _planner.FindCandidatesAsync(graph, request, cancellationToken: cancellationToken);
        if (paths.Count == 0)
        {
            return new IntelligentRouteSearchResult(
                IntelligentRouteStatuses.NoRoute,
                "No practical journey could satisfy the selected modes, walking limit, transfer limit and verified disruption constraints.",
                null);
        }

        var assembled = paths
            .Select(path => _assembler.Assemble(path, graph, request, paths.Count))
            .ToList();
        ApplyGeneralizedCosts(assembled, request);
        var top = SelectTopJourneys(assembled, request);
        if (top.Count == 0)
        {
            return new IntelligentRouteSearchResult(IntelligentRouteStatuses.NoRoute, "No eligible intelligent journey remained after ranking.", null);
        }

        var best = top[0].Dto;
        AddExplanations(top, best);
        var departure = await _departureWindow.EvaluateAsync(best, request, cancellationToken);
        var backup = SelectBackup(assembled, top[0]);
        var searchId = Guid.NewGuid();
        var warnings = BuildWarnings(baseRoutes.Response, assembled);

        _cache.Set(
            CacheKey(searchId),
            new IntelligentSearchCacheEntry
            {
                UserId = userId,
                Request = CloneRequest(request),
                Graph = graph,
                CandidatePool = assembled
            },
            TimeSpan.FromMinutes(_options.IntelligentSearchCacheMinutes));

        var response = new IntelligentRouteResponseDto
        {
            SearchId = searchId,
            DepartureTime = request.DepartureTime ?? DateTimeOffset.Now,
            Preference = request.Preference,
            Journeys = top.Select(x => x.Dto).ToArray(),
            BestOverallJourneyId = top[0].Dto.Id,
            FastestPracticalJourneyId = top.FirstOrDefault(x => string.Equals(x.Dto.Label, "FASTEST PRACTICAL", StringComparison.OrdinalIgnoreCase))?.Dto.Id
                ?? (top[0].Dto.Id == assembled.OrderBy(x => x.Dto.TotalDurationMinutes).First().Dto.Id ? top[0].Dto.Id : null),
            ResilientJourneyId = top.FirstOrDefault(x => string.Equals(x.Dto.Label, "RESILIENT / LOW-TRANSFER", StringComparison.OrdinalIgnoreCase))?.Dto.Id
                ?? (top[0].Dto.Id == assembled.OrderByDescending(x => x.Dto.Resilience).ThenBy(x => x.Dto.TransferCount).First().Dto.Id ? top[0].Dto.Id : null),
            BackupJourney = backup,
            DepartureWindow = departure,
            TrafficModelName = _traffic.ModelName,
            TrafficModelVersion = _traffic.ModelVersion,
            TrafficDataStatus = _trafficData.DataStatus,
            Warnings = warnings,
            GeneratedAtUtc = DateTime.UtcNow
        };

        return new IntelligentRouteSearchResult(IntelligentRouteStatuses.Success, null, response);
    }

    public async Task<(string Status, string? Message, WhatIfDisruptionResponseDto? Response)> SimulateDisruptionAsync(
        WhatIfDisruptionRequest request,
        ulong? userId,
        CancellationToken cancellationToken = default)
    {
        if (!_cache.TryGetValue<IntelligentSearchCacheEntry>(CacheKey(request.SearchId), out var cached) || cached is null)
        {
            return (IntelligentRouteStatuses.SearchExpired, "This intelligent route search has expired. Run it again before simulating a disruption.", null);
        }
        if (cached.UserId.HasValue && cached.UserId != userId)
        {
            return (IntelligentRouteStatuses.Forbidden, "This intelligent route search belongs to a different authenticated session.", null);
        }

        var journey = cached.CandidatePool.FirstOrDefault(x => x.Dto.Id == request.JourneyId);
        if (journey is null || !journey.LegEdgeIds.TryGetValue(request.LegSequence, out var blocked) || blocked.Count == 0)
        {
            return (IntelligentRouteStatuses.Invalid, "Choose a road/transit leg from the selected journey for the what-if blockage simulation.", null);
        }

        var paths = await _planner.FindCandidatesAsync(cached.Graph, cached.Request, blocked, cancellationToken);
        if (paths.Count == 0)
        {
            return (IntelligentRouteStatuses.NoRoute, "Under this what-if blockage, no practical alternative was found with the selected mobility constraints.", new WhatIfDisruptionResponseDto
            {
                SearchId = request.SearchId,
                Scenario = $"WHAT-IF SIMULATION: journey leg {request.LegSequence} treated as blocked.",
                Message = "No alternative could be confirmed for this simulated disruption."
            });
        }

        var assembled = paths.Select(p => _assembler.Assemble(p, cached.Graph, cached.Request, paths.Count)).ToList();
        ApplyGeneralizedCosts(assembled, cached.Request);
        var top = SelectTopJourneys(assembled, cached.Request);
        if (top.Count > 0)
        {
            AddExplanations(top, top[0].Dto);
        }

        return (IntelligentRouteStatuses.Success, null, new WhatIfDisruptionResponseDto
        {
            SearchId = request.SearchId,
            Scenario = $"WHAT-IF SIMULATION: journey leg {request.LegSequence} treated as blocked.",
            Journeys = top.Select(x => x.Dto).ToArray(),
            RecommendedJourneyId = top.FirstOrDefault()?.Dto.Id,
            Message = "Simulation only — this does not mean a real road blockage exists."
        });
    }

    private string? ValidateAndNormalize(IntelligentRouteRequest request)
    {
        if (!GeoMath.IsValidCoordinate(request.Start.Latitude, request.Start.Longitude)
            || !GeoMath.IsValidCoordinate(request.Destination.Latitude, request.Destination.Longitude))
        {
            return "Start and destination must contain valid latitude/longitude values.";
        }

        var separation = GeoMath.HaversineDistanceKm(
            request.Start.Latitude,
            request.Start.Longitude,
            request.Destination.Latitude,
            request.Destination.Longitude) * 1000d;
        if (separation < MinimumEndpointSeparationMeters)
        {
            return "Start and destination are effectively the same location.";
        }

        request.EnabledModes = request.EnabledModes
            .Select(m => m?.Trim().ToUpperInvariant() ?? string.Empty)
            .Where(MobilityModes.All.Contains)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (request.EnabledModes.Count == 0)
        {
            return "Enable at least one supported mobility mode.";
        }

        request.Preference = request.Preference?.Trim().ToUpperInvariant() ?? JourneyPreferences.Balanced;
        if (!JourneyPreferences.All.Contains(request.Preference))
        {
            return "The selected routing preference is not supported.";
        }
        if (request.MaxTransfers is < 0 or > 2)
        {
            return "Maximum transfers must be 0, 1 or 2.";
        }
        if (request.MaxWalkingMeters < 0 || request.MaxWalkingMeters > _options.MaximumWalkingMeters)
        {
            return $"Maximum walking distance must be 0 (no walking) or up to {_options.MaximumWalkingMeters} metres.";
        }

        var departure = request.DepartureTime ?? DateTimeOffset.Now;
        if (departure < DateTimeOffset.Now.AddHours(-2)
            || departure > DateTimeOffset.Now.AddHours(_options.MaxDepartureHoursAhead))
        {
            return $"Departure time must be current/recent or within the next {_options.MaxDepartureHoursAhead} hours.";
        }
        request.DepartureTime = departure;
        return null;
    }

    private void ApplyGeneralizedCosts(IReadOnlyList<AssembledJourney> journeys, IntelligentRouteRequest request)
    {
        var weights = JourneyRankingPolicy.For(request.Preference);
        var minTime = journeys.Min(x => x.Dto.TotalDurationMinutes);
        var maxTime = journeys.Max(x => x.Dto.TotalDurationMinutes);
        var minDistance = journeys.Min(x => x.Dto.TotalDistanceMeters);
        var maxDistance = journeys.Max(x => x.Dto.TotalDistanceMeters);

        foreach (var assembled in journeys)
        {
            var option = assembled.Dto;
            var safetyPenalty = 100d - (option.SafetyScore ?? 50d);
            var timePenalty = Normalize(option.TotalDurationMinutes, minTime, maxTime);
            var congestionPenalty = option.PredictedCongestionIndex;
            var transferPenalty = Math.Min(100d, option.TransferCount / (double)Math.Max(1, request.MaxTransfers) * 100d);
            var walkingPenalty = Math.Min(100d, option.WalkingDistanceMeters / Math.Max(1d, request.MaxWalkingMeters) * 100d);
            var distancePenalty = Normalize(option.TotalDistanceMeters, minDistance, maxDistance);
            var resiliencePenalty = 100d - option.Resilience;

            var weighted = (
                safetyPenalty * weights.Safety
                + timePenalty * weights.TravelTime
                + congestionPenalty * weights.Congestion
                + transferPenalty * weights.Transfers
                + walkingPenalty * weights.Walking
                + distancePenalty * weights.Distance
                + resiliencePenalty * weights.Resilience) / 100d;
            var unknownPenalty = (100d - option.DataConfidence) / 100d * _options.UnknownDataPenalty;
            option.GeneralizedCost = Math.Round(weighted + unknownPenalty, 2);
        }
    }

    private List<AssembledJourney> SelectTopJourneys(IReadOnlyList<AssembledJourney> pool, IntelligentRouteRequest request)
    {
        var ordered = pool.OrderBy(x => x.Dto.GeneralizedCost).ThenBy(x => x.Dto.TotalDurationMinutes).ToList();
        var selected = new List<AssembledJourney>();
        var best = ordered[0];
        selected.Add(best);
        best.Dto.Label = "BEST OVERALL";

        var fastest = pool.OrderBy(x => x.Dto.TotalDurationMinutes).ThenBy(x => x.Dto.GeneralizedCost).First();
        if (fastest.Dto.Id != best.Dto.Id && IsDistinctEnough(fastest, selected, _options.DiversityThreshold))
        {
            fastest.Dto.Label = "FASTEST PRACTICAL";
            selected.Add(fastest);
        }

        var resilient = pool.OrderByDescending(x => x.Dto.Resilience)
            .ThenBy(x => x.Dto.TransferCount)
            .ThenBy(x => x.Dto.GeneralizedCost)
            .First();
        if (resilient.Dto.Id != best.Dto.Id
            && selected.All(x => x.Dto.Id != resilient.Dto.Id)
            && IsDistinctEnough(resilient, selected, _options.DiversityThreshold))
        {
            resilient.Dto.Label = "RESILIENT / LOW-TRANSFER";
            selected.Add(resilient);
        }

        foreach (var candidate in ordered)
        {
            if (selected.Count >= _options.TopJourneyCount) break;
            if (selected.Any(x => x.Dto.Id == candidate.Dto.Id)) continue;
            if (!IsDistinctEnough(candidate, selected, _options.DiversityThreshold)) continue;
            candidate.Dto.Label = "ALTERNATIVE";
            selected.Add(candidate);
        }

        foreach (var candidate in ordered)
        {
            if (selected.Count >= _options.TopJourneyCount) break;
            if (selected.Any(x => x.Dto.Id == candidate.Dto.Id)) continue;
            candidate.Dto.Label = "ALTERNATIVE";
            selected.Add(candidate);
        }

        if (fastest.Dto.Id == best.Dto.Id)
        {
            best.Dto.Label = "BEST OVERALL";
        }
        if (resilient.Dto.Id == best.Dto.Id)
        {
            best.Dto.Label = "BEST OVERALL";
        }
        return selected.Take(_options.TopJourneyCount).ToList();
    }

    private void AddExplanations(IReadOnlyList<AssembledJourney> top, JourneyOptionDto best)
    {
        var all = top.Select(x => x.Dto).ToArray();
        foreach (var journey in all)
        {
            journey.Explanation = _explanations.ExplainRecommended(journey, all);
            journey.Tradeoffs = journey.Id == best.Id
                ? all.Where(x => x.Id != best.Id)
                    .SelectMany(other => _explanations.ExplainTradeoffs(other, best)
                        .Select(t => t with { Title = $"Why not {other.Label}: {t.Title}" }))
                    .ToArray()
                : _explanations.ExplainTradeoffs(journey, best);
        }
    }

    private JourneyOptionDto? SelectBackup(IReadOnlyList<AssembledJourney> pool, AssembledJourney primary)
    {
        var backup = pool
            .Where(x => x.Dto.Id != primary.Dto.Id)
            .Where(x => Similarity(primary, x) <= _options.BackupDiversityThreshold)
            .OrderBy(x => x.Dto.GeneralizedCost)
            .FirstOrDefault();
        return backup is null ? null : CloneWithLabel(backup.Dto, "BACKUP PLAN");
    }

    private IReadOnlyList<string> BuildWarnings(RouteSearchResponseDto baseRoutes, IReadOnlyList<AssembledJourney> journeys)
    {
        var warnings = new List<string>
        {
            "Traffic values are predictions/estimates based on a synthetic AUST-area development dataset, not measured live traffic.",
            "Bus stops, routes and frequencies in this build are synthetic development data, not an authoritative transit feed."
        };
        if (!_traffic.ModelAvailable)
        {
            warnings.Add("The trained ML.NET traffic artifact is not present; the documented synthetic-pattern fallback hierarchy is being used.");
        }
        if (journeys.SelectMany(x => x.Dto.Legs).Any(l => l.SafetySource == "VERIFIED_INCIDENT_PROXY"))
        {
            warnings.Add("Full road-segment Safety Score coverage is unavailable for some legs; those legs use a verified-incident development proxy and reduced data confidence.");
        }
        if (!baseRoutes.IncidentCheckAvailable)
        {
            warnings.Add("Verified incident information was temporarily unavailable for one or more provider routes.");
        }
        return warnings;
    }

    private static bool IsDistinctEnough(AssembledJourney candidate, IReadOnlyList<AssembledJourney> selected, double threshold) =>
        selected.All(existing => Similarity(existing, candidate) < threshold);

    private static double Similarity(AssembledJourney a, AssembledJourney b) =>
        JourneySimilarity.Calculate(a.Path, b.Path);

    private static double Normalize(double value, double min, double max) =>
        Math.Abs(max - min) < 0.000001d ? 0d : Math.Clamp((value - min) / (max - min) * 100d, 0d, 100d);

    private static IntelligentRouteRequest CloneRequest(IntelligentRouteRequest request) => new()
    {
        Start = request.Start with { },
        Destination = request.Destination with { },
        DepartureTime = request.DepartureTime,
        EnabledModes = request.EnabledModes.ToList(),
        Preference = request.Preference,
        MaxWalkingMeters = request.MaxWalkingMeters,
        MaxTransfers = request.MaxTransfers
    };

    private static JourneyOptionDto CloneWithLabel(JourneyOptionDto source, string label) => new()
    {
        Id = source.Id,
        Label = label,
        TotalDistanceMeters = source.TotalDistanceMeters,
        TotalDurationMinutes = source.TotalDurationMinutes,
        Modes = source.Modes,
        TransferCount = source.TransferCount,
        WalkingDistanceMeters = source.WalkingDistanceMeters,
        PredictedCongestionIndex = source.PredictedCongestionIndex,
        TrafficLevel = source.TrafficLevel,
        SafetyScore = source.SafetyScore,
        SafetyExposure = source.SafetyExposure,
        DataConfidence = source.DataConfidence,
        Resilience = source.Resilience,
        HazardCount = source.HazardCount,
        GeneralizedCost = source.GeneralizedCost,
        Legs = source.Legs,
        Explanation = source.Explanation,
        Tradeoffs = source.Tradeoffs,
        JourneyDna = source.JourneyDna,
        SpatialSignatureLengthMeters = source.SpatialSignatureLengthMeters
    };

    private static string CacheKey(Guid id) => $"intelligent-route:{id:N}";
}
