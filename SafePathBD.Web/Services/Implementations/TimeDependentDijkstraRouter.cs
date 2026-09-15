using Microsoft.Extensions.Options;
using SafePathBD.Web.Common;
using SafePathBD.Web.Models.DTOs.IntelligentRouting;
using SafePathBD.Web.Models.DTOs.Routing;
using SafePathBD.Web.Models.DTOs.Traffic;
using SafePathBD.Web.Services.IntelligentRouting;
using SafePathBD.Web.Services.Interfaces;

namespace SafePathBD.Web.Services.Implementations;

/// <summary>
/// Time-dependent Dijkstra over the request-scoped multimodal graph. Edge travel time is
/// evaluated at the arrival time of that edge, not once at the original departure time.
/// </summary>
public sealed class TimeDependentDijkstraRouter : ITimeDependentJourneyRouter
{
    private const double WalkBucketMeters = 250d;
    private readonly IVehicleTravelTimeService _travelTime;
    private readonly ISegmentSafetyProvider _safety;
    private readonly ITransitNetworkService _transit;
    private readonly IntelligentMobilityOptions _options;

    public TimeDependentDijkstraRouter(
        IVehicleTravelTimeService travelTime,
        ISegmentSafetyProvider safety,
        ITransitNetworkService transit,
        IOptions<IntelligentMobilityOptions> options)
    {
        _travelTime = travelTime;
        _safety = safety;
        _transit = transit;
        _options = options.Value;
    }

    public async Task<JourneyPath?> FindBestAsync(
        MobilityGraph graph,
        IntelligentRouteRequest request,
        IReadOnlySet<int>? excludedEdgeIds = null,
        string? singleModeOnly = null,
        CancellationToken cancellationToken = default)
    {
        var departure = request.DepartureTime ?? DateTimeOffset.Now;
        var enabledModes = request.EnabledModes
            .Select(m => m.Trim().ToUpperInvariant())
            .Where(MobilityModes.All.Contains)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(singleModeOnly))
        {
            enabledModes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                singleModeOnly.Trim().ToUpperInvariant()
            };
        }

        var weights = JourneyRankingPolicy.For(request.Preference);
        var queue = new PriorityQueue<RouteLabelKey, double>();
        var labels = new Dictionary<RouteLabelKey, RouteLabel>();

        void Seed(string mode, string? busRouteId = null, double initialMinutes = 0, double initialWalkingMeters = 0, double expectedWaitMinutes = 0)
        {
            var walkBucket = (int)Math.Floor(initialWalkingMeters / WalkBucketMeters);
            var key = new RouteLabelKey(graph.SourceNodeId, mode, busRouteId, 0, walkBucket);
            var label = new RouteLabel(
                Cost: initialMinutes,
                ArrivalTime: departure.AddMinutes(initialMinutes),
                WalkingMeters: initialWalkingMeters,
                Previous: null,
                Step: busRouteId is null ? null : new JourneyPathStep(
                    0,
                    graph.SourceNodeId,
                    graph.SourceNodeId,
                    MobilityModes.Bus,
                    null,
                    initialMinutes,
                    expectedWaitMinutes,
                    null,
                    null,
                    initialWalkingMeters > 0
                        ? $"Walk about {initialWalkingMeters:0} m to the synthetic development bus stop, then board"
                        : "Board synthetic development bus",
                    busRouteId,
                    _transit.Routes.FirstOrDefault(r => r.RouteId.Equals(busRouteId, StringComparison.OrdinalIgnoreCase))?.RouteName,
                    initialWalkingMeters));
            labels[key] = label;
            queue.Enqueue(key, label.Cost);
        }

        foreach (var mode in enabledModes.Where(m => m != MobilityModes.Bus))
        {
            Seed(mode);
        }

        if (enabledModes.Contains(MobilityModes.Bus))
        {
            var source = graph.Nodes[graph.SourceNodeId];
            foreach (var route in RoutesAtNode(source).DistinctBy(r => r.RouteId, StringComparer.OrdinalIgnoreCase))
            {
                var wait = _transit.GetExpectedWaitMinutes(route.RouteId, departure);
                var accessMeters = AccessMetersForRoute(source, route.RouteId);
                if (accessMeters > request.MaxWalkingMeters)
                {
                    continue;
                }
                var accessMinutes = WalkingMinutes(accessMeters);
                Seed(MobilityModes.Bus, route.RouteId, wait + _options.BusTransferMinutes + accessMinutes, accessMeters, wait);
            }
        }

        while (queue.TryDequeue(out var key, out var queuedCost))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!labels.TryGetValue(key, out var current) || queuedCost > current.Cost + 0.000001)
            {
                continue;
            }

            if (key.NodeId == graph.DestinationNodeId)
            {
                if (key.Mode == MobilityModes.Bus)
                {
                    var egressMeters = AccessMetersForRoute(graph.Nodes[key.NodeId], key.BusRouteId);
                    var finalWalking = current.WalkingMeters + egressMeters;
                    if (finalWalking > request.MaxWalkingMeters)
                    {
                        continue;
                    }
                    if (egressMeters > 0.5d)
                    {
                        var egressMinutes = WalkingMinutes(egressMeters);
                        var walkingFactor = Math.Max(0.25d, weights.Walking / 5d);
                        var timeFactor = Math.Max(0.25d, weights.TravelTime / 25d);
                        var final = new RouteLabel(
                            current.Cost + egressMinutes * timeFactor + egressMeters / 1000d * 1.6d * walkingFactor,
                            current.ArrivalTime.AddMinutes(egressMinutes),
                            finalWalking,
                            key,
                            new JourneyPathStep(
                                0, key.NodeId, key.NodeId, MobilityModes.Walk, null, egressMinutes, 0, null, null,
                                $"Walk about {egressMeters:0} m from the synthetic development bus stop to the destination",
                                key.BusRouteId,
                                _transit.Routes.FirstOrDefault(r => r.RouteId.Equals(key.BusRouteId, StringComparison.OrdinalIgnoreCase))?.RouteName,
                                egressMeters));
                        return Reconstruct(key, final, labels);
                    }
                }
                return Reconstruct(key, current, labels);
            }

            if (graph.Outgoing.TryGetValue(key.NodeId, out var outgoing))
            {
                foreach (var edge in outgoing)
                {
                    if (excludedEdgeIds?.Contains(edge.Id) == true)
                    {
                        continue;
                    }

                    if (!CanTraverse(edge, key.Mode, key.BusRouteId))
                    {
                        continue;
                    }

                    var evaluation = await EvaluateEdgeAsync(edge, key.Mode, current.ArrivalTime, weights, cancellationToken);
                    if (evaluation is null || evaluation.Safety.IsHardBlocked || !evaluation.Edge.Geometry.Any())
                    {
                        continue;
                    }

                    var walking = current.WalkingMeters + (key.Mode == MobilityModes.Walk ? edge.DistanceMeters : 0d);
                    if (walking > request.MaxWalkingMeters)
                    {
                        continue;
                    }

                    var bucket = (int)Math.Floor(walking / WalkBucketMeters);
                    var nextKey = new RouteLabelKey(edge.ToNodeId, key.Mode, key.BusRouteId, key.Transfers, bucket);
                    var nextCost = current.Cost + evaluation.GeneralizedEdgeCost;
                    var next = new RouteLabel(
                        nextCost,
                        current.ArrivalTime.AddMinutes(evaluation.DurationMinutes),
                        walking,
                        key,
                        new JourneyPathStep(
                            0,
                            edge.FromNodeId,
                            edge.ToNodeId,
                            key.Mode,
                            edge,
                            evaluation.DurationMinutes,
                            evaluation.ExpectedWaitMinutes,
                            evaluation.Traffic,
                            evaluation.Safety,
                            evaluation.TransferNote,
                            busEdgeRouteId(edge),
                            evaluation.BusRouteName));
                    Relax(nextKey, next, labels, queue);
                }
            }

            if (key.Transfers < request.MaxTransfers && string.IsNullOrWhiteSpace(singleModeOnly))
            {
                foreach (var transfer in BuildTransfers(graph.Nodes[key.NodeId], key, current.ArrivalTime, enabledModes))
                {
                    var nextTransfers = key.Transfers + 1;
                    if (nextTransfers > request.MaxTransfers)
                    {
                        continue;
                    }

                    var nextWalking = current.WalkingMeters + transfer.WalkingMeters;
                    if (nextWalking > request.MaxWalkingMeters)
                    {
                        continue;
                    }
                    var nextWalkBucket = (int)Math.Floor(nextWalking / WalkBucketMeters);
                    var nextKey = new RouteLabelKey(
                        key.NodeId,
                        transfer.Mode,
                        transfer.BusRouteId,
                        nextTransfers,
                        nextWalkBucket);
                    var transferPenalty = transfer.DurationMinutes
                        + (weights.Transfers / 8d) * 1.25d
                        + (transfer.WalkingMeters > 0 ? weights.Walking / 5d * transfer.WalkingMeters / 1000d : 0d);
                    var next = new RouteLabel(
                        current.Cost + transferPenalty,
                        current.ArrivalTime.AddMinutes(transfer.DurationMinutes),
                        nextWalking,
                        key,
                        new JourneyPathStep(
                            0,
                            key.NodeId,
                            key.NodeId,
                            transfer.Mode,
                            null,
                            transfer.DurationMinutes,
                            transfer.ExpectedWaitMinutes,
                            null,
                            null,
                            transfer.Note,
                            transfer.BusRouteId,
                            transfer.RouteName,
                            transfer.WalkingMeters));
                    Relax(nextKey, next, labels, queue);
                }
            }
        }

        return null;
    }

    private async Task<TraversalEvaluation?> EvaluateEdgeAsync(
        MobilityEdge edge,
        string mode,
        DateTimeOffset at,
        JourneyRankingWeights weights,
        CancellationToken cancellationToken)
    {
        var midpoint = edge.Geometry[edge.Geometry.Count / 2];
        var request = new TrafficPredictionRequest(
            edge.TrafficRoadId,
            midpoint.Latitude,
            midpoint.Longitude,
            at,
            _options.DefaultWeatherCondition,
            RoadClass: edge.RoadClass,
            Area: edge.Area,
            MatchDistanceMeters: double.IsFinite(edge.TrafficRoadMatchMeters) ? edge.TrafficRoadMatchMeters : null);

        var busEdge = edge as BusMobilityEdge;
        var travel = await _travelTime.EstimateAsync(
            mode,
            edge.DistanceMeters,
            request,
            busEdge?.RouteId,
            includeBusWait: false,
            cancellationToken);
        if (!travel.IsAvailable || travel.TravelMinutes < 0)
        {
            return null;
        }

        var safety = await _safety.GetSafetyAsync(edge.TrafficRoadId, edge.Geometry, edge.Incidents, cancellationToken);
        var movementMinutes = travel.TravelMinutes + (busEdge is null ? 0d : _options.BusStopDwellMinutes);
        var safetyScore = safety.SafetyScore ?? 50d;
        var confidencePenalty = (1d - Math.Clamp(safety.Coverage, 0d, 1d)) * _options.UnknownDataPenalty;
        confidencePenalty += ConfidencePenalty(travel.Traffic.TrafficDataConfidence) * _options.UnknownDataPenalty * 0.5d;

        var timeFactor = Math.Max(0.25d, weights.TravelTime / 25d);
        var safetyFactor = Math.Max(0.25d, weights.Safety / 45d);
        var congestionFactor = Math.Max(0.25d, weights.Congestion / 10d);
        var walkingFactor = Math.Max(0.25d, weights.Walking / 5d);
        var distanceFactor = Math.Max(0.25d, weights.Distance / 3d);

        var cost = movementMinutes * timeFactor;
        cost += (100d - safetyScore) / 100d * Math.Max(1d, movementMinutes) * 2.2d * safetyFactor;
        cost += travel.Traffic.PredictedCongestionIndex / 100d * Math.Max(1d, movementMinutes) * 0.9d * congestionFactor;
        cost += edge.DistanceMeters / 1000d * 0.35d * distanceFactor;
        if (mode == MobilityModes.Walk)
        {
            cost += edge.DistanceMeters / 1000d * 1.6d * walkingFactor;
        }
        if (safety.HazardState == RouteIncidentStates.Affected)
        {
            cost += 24d * safetyFactor;
        }
        cost += confidencePenalty;

        return new TraversalEvaluation(
            edge,
            mode,
            movementMinutes,
            0,
            travel.Traffic,
            safety,
            cost,
            BusRouteName: busEdge?.RouteName);
    }

    private IEnumerable<TransferCandidate> BuildTransfers(
        MobilityGraphNode node,
        RouteLabelKey current,
        DateTimeOffset at,
        IReadOnlySet<string> enabledModes)
    {
        if (current.Mode == MobilityModes.Walk && enabledModes.Contains(MobilityModes.Rickshaw))
        {
            yield return new TransferCandidate(MobilityModes.Rickshaw, null, _options.GenericTransferMinutes, 0, "Transfer to rickshaw", null, 0);
        }
        if (current.Mode == MobilityModes.Rickshaw && enabledModes.Contains(MobilityModes.Walk))
        {
            yield return new TransferCandidate(MobilityModes.Walk, null, _options.GenericTransferMinutes, 0, "Continue on foot", null, 0);
        }

        var busRoutes = RoutesAtNode(node);
        if (enabledModes.Contains(MobilityModes.Bus) && current.Mode is MobilityModes.Walk or MobilityModes.Rickshaw)
        {
            foreach (var route in busRoutes)
            {
                var wait = _transit.GetExpectedWaitMinutes(route.RouteId, at);
                var accessMeters = AccessMetersForRoute(node, route.RouteId);
                var accessMinutes = WalkingMinutes(accessMeters);
                yield return new TransferCandidate(
                    MobilityModes.Bus,
                    route.RouteId,
                    _options.BusTransferMinutes + wait + accessMinutes,
                    wait,
                    accessMeters > 0
                        ? $"Walk about {accessMeters:0} m to bus {route.RouteShortName}; expected wait ~{wait:0.#} min"
                        : $"Transfer to bus {route.RouteShortName}; expected wait ~{wait:0.#} min",
                    route.RouteName,
                    accessMeters);
            }
        }

        if (current.Mode == MobilityModes.Bus)
        {
            if (enabledModes.Contains(MobilityModes.Walk))
            {
                var accessMeters = AccessMetersForRoute(node, current.BusRouteId);
                yield return new TransferCandidate(MobilityModes.Walk, null, _options.GenericTransferMinutes + WalkingMinutes(accessMeters), 0, "Alight and continue on foot", null, accessMeters);
            }
            if (enabledModes.Contains(MobilityModes.Rickshaw))
            {
                var accessMeters = AccessMetersForRoute(node, current.BusRouteId);
                yield return new TransferCandidate(MobilityModes.Rickshaw, null, _options.GenericTransferMinutes + WalkingMinutes(accessMeters), 0, "Alight and transfer to rickshaw", null, accessMeters);
            }

            foreach (var route in busRoutes.Where(r => !string.Equals(r.RouteId, current.BusRouteId, StringComparison.OrdinalIgnoreCase)))
            {
                var wait = _transit.GetExpectedWaitMinutes(route.RouteId, at);
                var accessMeters = AccessMetersForRoute(node, route.RouteId);
                yield return new TransferCandidate(
                    MobilityModes.Bus,
                    route.RouteId,
                    _options.BusTransferMinutes + wait + WalkingMinutes(accessMeters),
                    wait,
                    $"Change to bus {route.RouteShortName}; expected wait ~{wait:0.#} min",
                    route.RouteName,
                    accessMeters);
            }
        }
    }

    private IReadOnlyList<TransitRouteInfo> RoutesAtNode(MobilityGraphNode node)
    {
        if (node.TransitStopIds.Count == 0)
        {
            return Array.Empty<TransitRouteInfo>();
        }
        return node.TransitStopIds
            .SelectMany(_transit.GetRoutesServingStop)
            .DistinctBy(r => r.RouteId, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private double AccessMetersForRoute(MobilityGraphNode node, string? routeId)
    {
        if (string.IsNullOrWhiteSpace(routeId) || node.TransitStopAccessMeters.Count == 0)
        {
            return 0d;
        }
        var candidates = node.TransitStopAccessMeters
            .Where(pair => _transit.GetRoutesServingStop(pair.Key)
                .Any(route => string.Equals(route.RouteId, routeId, StringComparison.OrdinalIgnoreCase)))
            .Select(pair => pair.Value)
            .ToArray();
        return candidates.Length == 0 ? 0d : candidates.Min();
    }

    private static double WalkingMinutes(double meters) => meters <= 0 ? 0d : (meters / 1000d) / 4.8d * 60d;

    private static bool CanTraverse(MobilityEdge edge, string mode, string? busRouteId) => edge switch
    {
        RoadMobilityEdge road => mode != MobilityModes.Bus && road.AllowedModes.Contains(mode),
        BusMobilityEdge bus => mode == MobilityModes.Bus && string.Equals(bus.RouteId, busRouteId, StringComparison.OrdinalIgnoreCase),
        _ => false
    };


    private static string? busEdgeRouteId(MobilityEdge edge) => (edge as BusMobilityEdge)?.RouteId;

    private static double ConfidencePenalty(string confidence) => confidence switch
    {
        DataConfidenceLevels.High => 0,
        DataConfidenceLevels.Medium => 0.35,
        _ => 0.7
    };

    private static void Relax(
        RouteLabelKey key,
        RouteLabel candidate,
        IDictionary<RouteLabelKey, RouteLabel> labels,
        PriorityQueue<RouteLabelKey, double> queue)
    {
        if (labels.TryGetValue(key, out var existing) && existing.Cost <= candidate.Cost + 0.000001)
        {
            return;
        }
        labels[key] = candidate;
        queue.Enqueue(key, candidate.Cost);
    }

    private static JourneyPath Reconstruct(
        RouteLabelKey destinationKey,
        RouteLabel destination,
        IReadOnlyDictionary<RouteLabelKey, RouteLabel> labels)
    {
        var steps = new List<JourneyPathStep>();
        var key = destinationKey;
        var label = destination;
        while (label.Previous.HasValue)
        {
            if (label.Step is not null)
            {
                steps.Add(label.Step);
            }
            key = label.Previous.Value;
            label = labels[key];
        }
        if (label.Step is not null)
        {
            steps.Add(label.Step);
        }
        steps.Reverse();
        steps = steps.Select((step, index) => step with { Sequence = index + 1 }).ToList();

        return new JourneyPath
        {
            Steps = steps,
            GeneralizedSearchCost = destination.Cost,
            ArrivalTime = destination.ArrivalTime,
            TransferCount = destinationKey.Transfers,
            WalkingMeters = destination.WalkingMeters
        };
    }

    private readonly record struct RouteLabelKey(
        int NodeId,
        string Mode,
        string? BusRouteId,
        int Transfers,
        int WalkBucket);

    private sealed record RouteLabel(
        double Cost,
        DateTimeOffset ArrivalTime,
        double WalkingMeters,
        RouteLabelKey? Previous,
        JourneyPathStep? Step);

    private sealed record TransferCandidate(
        string Mode,
        string? BusRouteId,
        double DurationMinutes,
        double ExpectedWaitMinutes,
        string Note,
        string? RouteName,
        double WalkingMeters);
}
