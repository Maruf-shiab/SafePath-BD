using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using SafePathBD.Web.Common;
using SafePathBD.Web.Data;
using SafePathBD.Web.Integrations.Routing;
using SafePathBD.Web.Models.DTOs.Routing;
using SafePathBD.Web.Models.DTOs.Traffic;
using SafePathBD.Web.Services.Interfaces;

namespace SafePathBD.Web.Services.Implementations;

public sealed class RoutingService : IRoutingService
{
    private const double MinimumEndpointSeparationMeters = 25d;
    private const double DefaultMaxRouteSearchRadiusKm = 25d;
    private const string MaxRouteRadiusSettingKey = "default_route_search_radius_km";

    private readonly IRoutingProvider _provider;
    private readonly IRouteIncidentService _incidentService;
    private readonly SafePathDbContext _db;
    private readonly IMemoryCache _cache;
    private const double TrafficSampleMeters = 450d;

    private readonly OsrmRoutingOptions _options;
    private readonly ILogger<RoutingService> _logger;
    private readonly ITrafficPredictionService? _trafficPrediction;
    private readonly ITrafficDataRepository? _trafficData;

    public RoutingService(
        IRoutingProvider provider,
        IRouteIncidentService incidentService,
        SafePathDbContext db,
        IMemoryCache cache,
        IOptions<OsrmRoutingOptions> options,
        ILogger<RoutingService> logger,
        ITrafficPredictionService? trafficPrediction = null,
        ITrafficDataRepository? trafficData = null)
    {
        _provider = provider;
        _incidentService = incidentService;
        _db = db;
        _cache = cache;
        _options = options.Value;
        _logger = logger;
        _trafficPrediction = trafficPrediction;
        _trafficData = trafficData;
    }

    public async Task<RouteSearchResult> SearchAsync(
        RouteSearchRequest request,
        ulong? userId,
        CancellationToken cancellationToken = default)
    {
        var validation = await ValidateRequestAsync(request, cancellationToken);
        if (validation is not null)
        {
            return RouteSearchResult.Invalid(validation);
        }

        IReadOnlyList<ProviderRouteCandidate> providerRoutes;
        try
        {
            providerRoutes = await _provider.GetRoutesAsync(
                new RoutingProviderRequest(
                    ToCoordinate(request.Start),
                    ToCoordinate(request.Destination),
                    RequestAlternatives: true),
                cancellationToken);
        }
        catch (RoutingProviderNoRouteException ex)
        {
            return RouteSearchResult.NoRoute(ex.Message);
        }
        catch (RoutingProviderUnavailableException ex)
        {
            _logger.LogWarning(ex, "Routing provider failed while searching routes.");
            return RouteSearchResult.Unavailable(ex.Message);
        }

        if (providerRoutes.Count == 0)
        {
            return RouteSearchResult.NoRoute("No drivable route was found between these locations.");
        }

        var candidates = providerRoutes
            .Select((route, index) => ToCandidate(route, $"route-{index + 1}", isDetour: false))
            .ToList();

        MarkShortestAndFastest(candidates);

        var incidentCheckAvailable = true;
        var trafficEstimateAvailable = false;
        var trafficEstimatedAt = DateTimeOffset.Now;
        foreach (var candidate in candidates)
        {
            incidentCheckAvailable &= await AssessCandidateAsync(candidate, cancellationToken);
            trafficEstimateAvailable |= await AssessTrafficAsync(candidate, trafficEstimatedAt, cancellationToken);
        }

        var shortest = candidates.Single(c => c.IsShortest);
        var detourAttempted = false;
        string? detourMessage = null;

        // OSRM alternatives can overlap heavily. If the technical shortest route is seriously
        // affected and none of the provider alternatives is CLEAR, actively ask OSRM for a
        // road-routed avoidance corridor instead of accepting another near-identical route.
        if (_options.DetourFallbackEnabled
            && shortest.IncidentState == RouteIncidentStates.Affected
            && !candidates.Any(c => c.IncidentState == RouteIncidentStates.Clear))
        {
            detourAttempted = true;
            var detour = await TryBuildDetourAsync(
                request,
                shortest,
                candidates,
                cancellationToken);

            if (detour is not null)
            {
                trafficEstimateAvailable |= await AssessTrafficAsync(detour, trafficEstimatedAt, cancellationToken);
                candidates.Add(detour);
                incidentCheckAvailable &= detour.IncidentCheckAvailable;
                detourMessage = "A distinct road-routed alternative was generated around the verified incident.";
            }
            else
            {
                detourMessage = "No clear separate road alternative could be confirmed with the available road network.";
            }
        }

        // Recalculate flags after a generated detour is added. Shortest remains distance-based;
        // Fastest uses SafePath's predicted traffic ETA when available and provider ETA otherwise.
        MarkShortestAndFastest(candidates);
        shortest = candidates.Single(c => c.IsShortest);
        var recommended = ChooseRecommendedCandidate(candidates, shortest);
        recommended ??= shortest;
        recommended.IsRecommendedAlternative = recommended.CandidateKey != shortest.CandidateKey;

        var searchId = Guid.NewGuid();
        var checkedAt = DateTime.UtcNow;
        var response = new RouteSearchResponseDto
        {
            SearchId = searchId,
            Candidates = candidates,
            ShortestCandidateKey = shortest.CandidateKey,
            FastestCandidateKey = candidates.Single(c => c.IsFastest).CandidateKey,
            RecommendedCandidateKey = recommended.CandidateKey,
            IncidentCheckedAt = checkedAt,
            IncidentCheckAvailable = incidentCheckAvailable,
            IncidentMessage = incidentCheckAvailable
                ? null
                : "Incident information is temporarily unavailable for one or more routes.",
            HasAffectedRoute = candidates.Any(c => c.IncidentState == RouteIncidentStates.Affected),
            DetourAttempted = detourAttempted,
            DetourMessage = detourMessage,
            RecommendedDistanceDeltaKm = recommended.CandidateKey == shortest.CandidateKey
                ? 0
                : Math.Round((recommended.DistanceMeters - shortest.DistanceMeters) / 1000d, 2),
            RecommendedDurationDeltaMinutes = recommended.CandidateKey == shortest.CandidateKey
                ? 0
                : Math.Round((recommended.EffectiveDurationSeconds - shortest.EffectiveDurationSeconds) / 60d, 1),
            RecommendationReason = RecommendationReason(shortest, recommended),
            TrafficAwareRecommendation = trafficEstimateAvailable,
            TrafficEstimatedAt = trafficEstimateAvailable ? trafficEstimatedAt.UtcDateTime : null,
            IncidentRecheckSeconds = _options.IncidentRecheckSeconds
        };

        CacheSearch(searchId, request, candidates, userId);
        return RouteSearchResult.Success(response);
    }

    public async Task<RouteRecheckResult> RecheckAsync(
        RouteRecheckRequest request,
        ulong? userId,
        CancellationToken cancellationToken = default)
    {
        if (request.SearchId == Guid.Empty || string.IsNullOrWhiteSpace(request.SelectedCandidateKey))
        {
            return RouteRecheckResult.Invalid("A valid route search and selected route are required.");
        }

        if (!_cache.TryGetValue<RouteSearchCacheEntry>(CacheKey(request.SearchId), out var entry) || entry is null)
        {
            return RouteRecheckResult.Expired("This route search has expired. Find routes again to refresh it.");
        }

        if (entry.OwnerUserId.HasValue && entry.OwnerUserId != userId)
        {
            return RouteRecheckResult.Forbidden("This route search belongs to another signed-in session.");
        }

        if (!entry.Candidates.TryGetValue(request.SelectedCandidateKey, out var candidate))
        {
            return RouteRecheckResult.Invalid("The selected route is not part of this route search.");
        }

        try
        {
            var assessment = await _incidentService.AssessRouteAsync(candidate.Geometry, cancellationToken);
            var newIncidentIds = assessment.Incidents.Select(i => i.ReportId).OrderBy(id => id).ToArray();
            var changed = !string.Equals(candidate.IncidentState, assessment.IncidentState, StringComparison.Ordinal)
                          || !candidate.IncidentIds.SequenceEqual(newIncidentIds);

            var previous = candidate.IncidentState;
            candidate.IncidentState = assessment.IncidentState;
            candidate.IncidentIds = newIncidentIds;

            return RouteRecheckResult.Success(new RouteRecheckResponseDto
            {
                SearchId = request.SearchId,
                CandidateKey = request.SelectedCandidateKey,
                PreviousIncidentState = previous,
                IncidentState = assessment.IncidentState,
                Incidents = assessment.Incidents,
                IncidentCheckedAt = assessment.CheckedAt,
                IncidentChanged = changed,
                RerouteRecommended = assessment.IncidentState == RouteIncidentStates.Affected,
                IncidentCheckAvailable = true,
                Message = changed && assessment.IncidentState == RouteIncidentStates.Affected
                    ? "A newly verified road incident now affects this route."
                    : null
            });
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Incident recheck failed for cached route search {SearchId}.", request.SearchId);
            return RouteRecheckResult.Success(new RouteRecheckResponseDto
            {
                SearchId = request.SearchId,
                CandidateKey = request.SelectedCandidateKey,
                PreviousIncidentState = candidate.IncidentState,
                IncidentState = candidate.IncidentState,
                Incidents = Array.Empty<RouteIncidentDto>(),
                IncidentCheckedAt = DateTime.UtcNow,
                IncidentChanged = false,
                RerouteRecommended = candidate.IncidentState == RouteIncidentStates.Affected,
                IncidentCheckAvailable = false,
                Message = "Could not refresh incident information."
            });
        }
    }

    private async Task<string?> ValidateRequestAsync(RouteSearchRequest? request, CancellationToken cancellationToken)
    {
        if (request?.Start is null || request.Destination is null)
        {
            return "Start and destination are required.";
        }

        if (!GeoMath.IsValidCoordinate(request.Start.Latitude, request.Start.Longitude)
            || !GeoMath.IsValidCoordinate(request.Destination.Latitude, request.Destination.Longitude))
        {
            return "Start or destination coordinates are invalid.";
        }

        var separationKm = GeoMath.HaversineDistanceKm(
            request.Start.Latitude,
            request.Start.Longitude,
            request.Destination.Latitude,
            request.Destination.Longitude);

        if (separationKm * 1000d < MinimumEndpointSeparationMeters)
        {
            return "Start and destination are too close together to calculate a useful driving route.";
        }

        var maxRadiusKm = await GetMaximumSearchRadiusKmAsync(cancellationToken);
        if (separationKm > maxRadiusKm)
        {
            return $"The selected locations are more than {maxRadiusKm:0.#} km apart. Choose locations within the configured route-search area.";
        }

        return null;
    }

    private async Task<double> GetMaximumSearchRadiusKmAsync(CancellationToken cancellationToken)
    {
        var raw = await _db.SystemSettings.AsNoTracking()
            .Where(s => s.SettingKey == MaxRouteRadiusSettingKey)
            .Select(s => s.SettingValue)
            .FirstOrDefaultAsync(cancellationToken);

        return double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && value > 0
            ? value
            : DefaultMaxRouteSearchRadiusKm;
    }

    private async Task<bool> AssessCandidateAsync(RouteCandidateDto candidate, CancellationToken cancellationToken)
    {
        try
        {
            var assessment = await _incidentService.AssessRouteAsync(candidate.Geometry, cancellationToken);
            candidate.IncidentState = assessment.IncidentState;
            candidate.Incidents = assessment.Incidents;
            candidate.IncidentCheckAvailable = true;
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Incident assessment failed for route candidate {CandidateKey}.", candidate.CandidateKey);
            candidate.IncidentState = RouteIncidentStates.Unknown;
            candidate.Incidents = Array.Empty<RouteIncidentDto>();
            candidate.IncidentCheckAvailable = false;
            return false;
        }
    }

    private async Task<bool> AssessTrafficAsync(
        RouteCandidateDto candidate,
        DateTimeOffset departureTime,
        CancellationToken cancellationToken)
    {
        if (_trafficPrediction is null || _trafficData is null
            || candidate.Geometry.Count < 2
            || candidate.DistanceMeters <= 0)
        {
            return false;
        }

        try
        {
            var slices = BuildTrafficSlices(candidate.Geometry, TrafficSampleMeters);
            if (slices.Count == 0)
            {
                return false;
            }

            var providerSecondsPerMeter = candidate.DurationSeconds / Math.Max(1d, candidate.DistanceMeters);
            var at = departureTime;
            var totalSeconds = 0d;
            var matchedMeters = 0d;
            var weightedCongestion = 0d;
            var weightedConfidence = 0d;

            foreach (var slice in slices)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var match = _trafficData.FindNearestRoad(slice.SamplePoint.Latitude, slice.SamplePoint.Longitude);
                if (match?.RoadId is not { Length: > 0 })
                {
                    var fallbackSeconds = slice.DistanceMeters * providerSecondsPerMeter;
                    totalSeconds += fallbackSeconds;
                    at = at.AddSeconds(fallbackSeconds);
                    continue;
                }

                var prediction = await _trafficPrediction.PredictAsync(
                    new TrafficPredictionRequest(
                        match.RoadId,
                        slice.SamplePoint.Latitude,
                        slice.SamplePoint.Longitude,
                        at,
                        "Clear",
                        match.RoadConditionScore,
                        match.RoadClass,
                        match.Area,
                        match.DistanceFromAustKm,
                        match.DistanceMeters),
                    cancellationToken);

                var speedKph = _trafficData.GetCalibratedSpeedKph(
                    MobilityModes.Car,
                    prediction.RoadClass ?? match.RoadClass ?? "unknown",
                    prediction.PredictedCongestionIndex);
                speedKph = Math.Max(3d, speedKph);

                var seconds = slice.DistanceMeters / 1000d / speedKph * 3600d;
                totalSeconds += seconds;
                at = at.AddSeconds(seconds);
                matchedMeters += slice.DistanceMeters;
                weightedCongestion += prediction.PredictedCongestionIndex * slice.DistanceMeters;
                weightedConfidence += ConfidenceScore(prediction.TrafficDataConfidence) * slice.DistanceMeters;
            }

            if (matchedMeters <= 0d)
            {
                return false;
            }

            var coverage = Math.Clamp(matchedMeters / Math.Max(1d, candidate.DistanceMeters), 0d, 1d);
            var congestion = weightedCongestion / matchedMeters;
            var confidenceScore = weightedConfidence / matchedMeters;
            candidate.TrafficEstimateAvailable = true;
            candidate.PredictedCongestionIndex = Math.Round(congestion, 1);
            candidate.TrafficLevel = TrafficLevels.FromIndex(congestion);
            candidate.TrafficDataConfidence = coverage < 0.40d || confidenceScore < 1.5d
                ? DataConfidenceLevels.Low
                : coverage >= 0.75d && confidenceScore >= 2.5d
                    ? DataConfidenceLevels.High
                    : DataConfidenceLevels.Medium;
            candidate.TrafficAdjustedDurationSeconds = Math.Max(60d, totalSeconds);
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Traffic estimate failed for route candidate {CandidateKey}; provider ETA remains available.", candidate.CandidateKey);
            candidate.TrafficEstimateAvailable = false;
            candidate.PredictedCongestionIndex = null;
            candidate.TrafficLevel = null;
            candidate.TrafficDataConfidence = null;
            candidate.TrafficAdjustedDurationSeconds = null;
            return false;
        }
    }

    private static IReadOnlyList<TrafficSlice> BuildTrafficSlices(
        IReadOnlyList<RouteCoordinate> geometry,
        double targetMeters)
    {
        var slices = new List<TrafficSlice>();
        if (geometry.Count < 2)
        {
            return slices;
        }

        var accumulated = 0d;
        var weightedLatitude = 0d;
        var weightedLongitude = 0d;

        for (var i = 1; i < geometry.Count; i++)
        {
            var from = geometry[i - 1];
            var to = geometry[i];
            var meters = GeoMath.HaversineDistanceKm(
                from.Latitude, from.Longitude, to.Latitude, to.Longitude) * 1000d;
            if (meters <= 0d)
            {
                continue;
            }

            var midpoint = new RouteCoordinate(
                (from.Latitude + to.Latitude) / 2d,
                (from.Longitude + to.Longitude) / 2d);
            accumulated += meters;
            weightedLatitude += midpoint.Latitude * meters;
            weightedLongitude += midpoint.Longitude * meters;

            if (accumulated >= targetMeters)
            {
                slices.Add(new TrafficSlice(
                    accumulated,
                    new RouteCoordinate(weightedLatitude / accumulated, weightedLongitude / accumulated)));
                accumulated = 0d;
                weightedLatitude = 0d;
                weightedLongitude = 0d;
            }
        }

        if (accumulated > 0d)
        {
            slices.Add(new TrafficSlice(
                accumulated,
                new RouteCoordinate(weightedLatitude / accumulated, weightedLongitude / accumulated)));
        }

        return slices;
    }

    private static double ConfidenceScore(string? confidence) => confidence?.ToUpperInvariant() switch
    {
        DataConfidenceLevels.High => 3d,
        DataConfidenceLevels.Medium => 2d,
        _ => 1d
    };

    private RouteCandidateDto? ChooseRecommendedCandidate(
        IReadOnlyList<RouteCandidateDto> candidates,
        RouteCandidateDto shortest)
    {
        // Hard safety preference: never recommend an AFFECTED route while a CLEAR or CAUTION
        // alternative exists. Within the best incident tier, prefer predicted arrival time so
        // a slightly longer but less congested route can correctly win.
        var clear = candidates
            .Where(c => c.IncidentState == RouteIncidentStates.Clear)
            .OrderBy(c => c.EffectiveDurationSeconds)
            .ThenBy(c => c.DistanceMeters)
            .ThenBy(c => c.ProviderIndex)
            .FirstOrDefault();
        if (clear is not null)
        {
            return clear;
        }

        var caution = candidates
            .Where(c => c.IncidentState == RouteIncidentStates.Caution)
            .OrderBy(c => c.EffectiveDurationSeconds)
            .ThenBy(c => c.DistanceMeters)
            .ThenBy(c => c.ProviderIndex)
            .FirstOrDefault();
        if (caution is not null)
        {
            return caution;
        }

        // If incident data is unavailable, keep the technical shortest route rather than
        // pretending the route is clear. If every known candidate is affected and no detour
        // could be generated, the shortest route remains visible with a strong warning.
        if (!shortest.IncidentCheckAvailable || shortest.IncidentState == RouteIncidentStates.Unknown)
        {
            return shortest;
        }

        return candidates
            .OrderBy(c => c.EffectiveDurationSeconds)
            .ThenBy(c => c.DistanceMeters)
            .ThenBy(c => c.ProviderIndex)
            .FirstOrDefault();
    }

    private async Task<RouteCandidateDto?> TryBuildDetourAsync(
        RouteSearchRequest request,
        RouteCandidateDto shortest,
        IReadOnlyList<RouteCandidateDto> existing,
        CancellationToken cancellationToken)
    {
        var blockingIncident = shortest.Incidents
            .Where(i => i.IncidentLevel == RouteIncidentStates.Affected)
            .OrderByDescending(IncidentPriority)
            .ThenBy(i => i.DistanceFromRouteMeters)
            .FirstOrDefault();

        if (blockingIncident is null || shortest.Geometry.Count < 2)
        {
            return null;
        }

        var incidentPoint = new RouteCoordinate(blockingIncident.Latitude, blockingIncident.Longitude);
        var closest = RouteGeometryMath.ClosestPointOnPolyline(incidentPoint, shortest.Geometry);
        var segmentIndex = Math.Clamp(closest.SegmentIndex, 0, shortest.Geometry.Count - 2);
        var routeBearing = ResolveLocalBearing(shortest.Geometry, segmentIndex);

        RouteCandidateDto? cautionFallback = null;

        for (var attempt = 0; attempt < _options.MaxDetourAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Push OSRM around the incident with TWO same-side via points (before and after
            // the incident), not a single point that can snap back to the same blocked corridor.
            // Alternate sides and widen the corridor on later attempts.
            var side = attempt % 2 == 0 ? 90d : -90d;
            var multiplier = 1d + (attempt / 2) * 0.75d;
            var offsetMeters = _options.DetourOffsetMeters * multiplier;
            var anchorDistance = Math.Max(250d, Math.Min(900d, offsetMeters * 0.75d));
            var before = FindRouteAnchor(shortest.Geometry, segmentIndex, anchorDistance, backwards: true);
            var after = FindRouteAnchor(shortest.Geometry, segmentIndex + 1, anchorDistance, backwards: false);
            var sideBearing = routeBearing + side;
            var vias = new[]
            {
                RouteGeometryMath.DestinationPoint(before, offsetMeters, sideBearing),
                RouteGeometryMath.DestinationPoint(after, offsetMeters, sideBearing)
            };

            IReadOnlyList<ProviderRouteCandidate> routed;
            try
            {
                routed = await _provider.GetRoutesAsync(
                    new RoutingProviderRequest(
                        ToCoordinate(request.Start),
                        ToCoordinate(request.Destination),
                        vias,
                        RequestAlternatives: false),
                    cancellationToken);
            }
            catch (RoutingProviderNoRouteException)
            {
                continue;
            }
            catch (RoutingProviderUnavailableException ex)
            {
                _logger.LogInformation(ex, "Detour attempt {Attempt} could not be routed.", attempt + 1);
                continue;
            }

            foreach (var providerRoute in routed)
            {
                if (providerRoute.DistanceMeters > shortest.DistanceMeters * _options.MaxDetourDistanceFactor)
                {
                    continue;
                }

                if (existing.Any(existingCandidate => AreNearDuplicate(existingCandidate.Geometry, providerRoute.Geometry)))
                {
                    continue;
                }

                var candidate = ToCandidate(providerRoute, $"detour-{attempt + 1}", isDetour: true, providerIndex: 10_000 + attempt);
                var checkWorked = await AssessCandidateAsync(candidate, cancellationToken);
                if (!checkWorked || candidate.IncidentState == RouteIncidentStates.Affected)
                {
                    continue;
                }

                if (candidate.IncidentState == RouteIncidentStates.Clear)
                {
                    return candidate;
                }

                if (cautionFallback is null || candidate.DistanceMeters < cautionFallback.DistanceMeters)
                {
                    cautionFallback = candidate;
                }
            }
        }

        return cautionFallback;
    }

    private static RouteCoordinate FindRouteAnchor(
        IReadOnlyList<RouteCoordinate> geometry,
        int startIndex,
        double targetMeters,
        bool backwards)
    {
        var index = Math.Clamp(startIndex, 0, geometry.Count - 1);
        var travelled = 0d;
        var current = geometry[index];

        while (travelled < targetMeters)
        {
            var nextIndex = backwards ? index - 1 : index + 1;
            if (nextIndex < 0 || nextIndex >= geometry.Count)
            {
                break;
            }

            var next = geometry[nextIndex];
            travelled += GeoMath.HaversineDistanceKm(
                current.Latitude, current.Longitude, next.Latitude, next.Longitude) * 1000d;
            current = next;
            index = nextIndex;
        }

        return current;
    }

    private static double ResolveLocalBearing(IReadOnlyList<RouteCoordinate> geometry, int preferredSegmentIndex)
    {
        for (var offset = 0; offset < geometry.Count - 1; offset++)
        {
            var forward = preferredSegmentIndex + offset;
            if (forward >= 0 && forward < geometry.Count - 1
                && GeoMath.HaversineDistanceKm(
                    geometry[forward].Latitude, geometry[forward].Longitude,
                    geometry[forward + 1].Latitude, geometry[forward + 1].Longitude) * 1000d > 1d)
            {
                return RouteGeometryMath.BearingDegrees(geometry[forward], geometry[forward + 1]);
            }

            var backward = preferredSegmentIndex - offset;
            if (backward >= 0 && backward < geometry.Count - 1
                && GeoMath.HaversineDistanceKm(
                    geometry[backward].Latitude, geometry[backward].Longitude,
                    geometry[backward + 1].Latitude, geometry[backward + 1].Longitude) * 1000d > 1d)
            {
                return RouteGeometryMath.BearingDegrees(geometry[backward], geometry[backward + 1]);
            }
        }

        return 0d;
    }

    private static int IncidentPriority(RouteIncidentDto incident)
    {
        if (incident.ReportType == ReportTypes.Accident)
        {
            return incident.SeverityName?.ToUpperInvariant() switch
            {
                "FATAL" => 50,
                "SEVERE" => 40,
                _ => 10
            };
        }

        var roadBlock = string.Equals(incident.HazardType, "Road Block", StringComparison.OrdinalIgnoreCase);
        return incident.RiskLevel?.ToUpperInvariant() switch
        {
            "CRITICAL" when roadBlock => 45,
            "CRITICAL" => 35,
            "HIGH" when roadBlock => 30,
            _ => 10
        };
    }

    private static void MarkShortestAndFastest(IReadOnlyList<RouteCandidateDto> candidates)
    {
        foreach (var candidate in candidates)
        {
            candidate.IsShortest = false;
            candidate.IsFastest = false;
        }

        var shortest = candidates
            .OrderBy(c => c.DistanceMeters)
            .ThenBy(c => c.EffectiveDurationSeconds)
            .ThenBy(c => c.ProviderIndex)
            .First();

        var fastest = candidates
            .OrderBy(c => c.EffectiveDurationSeconds)
            .ThenBy(c => c.DistanceMeters)
            .ThenBy(c => c.ProviderIndex)
            .First();

        shortest.IsShortest = true;
        fastest.IsFastest = true;
    }

    private static RouteCandidateDto ToCandidate(
        ProviderRouteCandidate route,
        string key,
        bool isDetour,
        int? providerIndex = null) =>
        new()
        {
            CandidateKey = key,
            ProviderIndex = providerIndex ?? route.ProviderIndex,
            DistanceMeters = route.DistanceMeters,
            DurationSeconds = route.DurationSeconds,
            Geometry = route.Geometry,
            IsDetour = isDetour
        };

    private static RouteCoordinate ToCoordinate(RoutePointRequest point) =>
        new(point.Latitude, point.Longitude);

    private static bool AreNearDuplicate(
        IReadOnlyList<RouteCoordinate> left,
        IReadOnlyList<RouteCoordinate> right)
    {
        if (left.Count < 2 || right.Count < 2)
        {
            return false;
        }

        var indexes = new[] { 0.25d, 0.5d, 0.75d };
        return indexes.All(fraction =>
        {
            var index = Math.Clamp((int)Math.Round((left.Count - 1) * fraction), 0, left.Count - 1);
            return RouteGeometryMath.DistancePointToPolylineMeters(left[index], right) <= 80d;
        });
    }

    private static string RecommendationReason(RouteCandidateDto shortest, RouteCandidateDto recommended)
    {
        if (recommended.CandidateKey == shortest.CandidateKey)
        {
            return shortest.IncidentState switch
            {
                RouteIncidentStates.Clear when shortest.TrafficEstimateAvailable
                    => "The shortest route is also the best currently estimated arrival among clear routes using SafePath predicted traffic patterns.",
                RouteIncidentStates.Clear
                    => "The shortest route has no relevant verified incident currently detected near it.",
                RouteIncidentStates.Caution
                    => "No clear route was available, so this caution-level route is the best currently estimated option.",
                RouteIncidentStates.Affected
                    => "No clear or caution-level alternative could be confirmed, so the affected route remains visible with a strong warning.",
                _
                    => "This route remains selected because current incident information could not be fully checked."
            };
        }

        if (shortest.IncidentState == RouteIncidentStates.Affected
            && recommended.IncidentState == RouteIncidentStates.Clear)
        {
            return recommended.TrafficEstimateAvailable
                ? "Avoids the verified serious incident on the shortest route and is the quickest clear alternative under current predicted traffic patterns."
                : "Avoids the verified serious incident affecting the shortest route.";
        }

        if (shortest.IncidentState == RouteIncidentStates.Caution
            && recommended.IncidentState == RouteIncidentStates.Clear)
        {
            return "Uses a clear alternative instead of the caution-level incident route, while also considering estimated travel time.";
        }

        if (shortest.IncidentState == RouteIncidentStates.Clear
            && recommended.IncidentState == RouteIncidentStates.Clear
            && recommended.EffectiveDurationSeconds + 30d < shortest.EffectiveDurationSeconds)
        {
            return "This route is slightly longer but is predicted to reach the destination sooner because its current congestion estimate is lower.";
        }

        return "This is the best currently suitable route after verified incidents and predicted traffic are considered together.";
    }

    private void CacheSearch(
        Guid searchId,
        RouteSearchRequest request,
        IReadOnlyList<RouteCandidateDto> candidates,
        ulong? userId)
    {
        var entry = new RouteSearchCacheEntry
        {
            OwnerUserId = userId,
            Start = request.Start,
            Destination = request.Destination,
            Candidates = candidates.ToDictionary(
                c => c.CandidateKey,
                c => new CachedCandidate
                {
                    Geometry = c.Geometry,
                    IncidentState = c.IncidentState,
                    IncidentIds = c.Incidents.Select(i => i.ReportId).OrderBy(id => id).ToArray()
                },
                StringComparer.Ordinal)
        };

        _cache.Set(
            CacheKey(searchId),
            entry,
            new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(_options.CacheMinutes),
                Size = 1
            });
    }

    private static string CacheKey(Guid searchId) => $"routing-search:{searchId:N}";

    private sealed record TrafficSlice(double DistanceMeters, RouteCoordinate SamplePoint);

    private sealed class RouteSearchCacheEntry
    {
        public ulong? OwnerUserId { get; init; }
        public RoutePointRequest Start { get; init; } = null!;
        public RoutePointRequest Destination { get; init; } = null!;
        public Dictionary<string, CachedCandidate> Candidates { get; init; } = new(StringComparer.Ordinal);
    }

    private sealed class CachedCandidate
    {
        public IReadOnlyList<RouteCoordinate> Geometry { get; init; } = Array.Empty<RouteCoordinate>();
        public string IncidentState { get; set; } = RouteIncidentStates.Unknown;
        public ulong[] IncidentIds { get; set; } = Array.Empty<ulong>();
    }
}
