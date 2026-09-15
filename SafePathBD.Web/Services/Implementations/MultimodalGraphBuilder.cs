using Microsoft.Extensions.Options;
using SafePathBD.Web.Common;
using SafePathBD.Web.Models.DTOs.Routing;
using SafePathBD.Web.Services.IntelligentRouting;
using SafePathBD.Web.Services.Interfaces;

namespace SafePathBD.Web.Services.Implementations;

public sealed class MultimodalGraphBuilder : IMultimodalGraphBuilder
{
    private readonly ITrafficDataRepository _traffic;
    private readonly ITransitNetworkService _transit;
    private readonly IRickshawRoadEligibilityPolicy _rickshaw;
    private readonly IntelligentMobilityOptions _options;

    public MultimodalGraphBuilder(
        ITrafficDataRepository traffic,
        ITransitNetworkService transit,
        IRickshawRoadEligibilityPolicy rickshaw,
        IOptions<IntelligentMobilityOptions> options)
    {
        _traffic = traffic;
        _transit = transit;
        _rickshaw = rickshaw;
        _options = options.Value;
    }

    public MobilityGraph Build(IReadOnlyList<RouteCandidateDto> candidates)
    {
        if (candidates.Count == 0 || candidates.All(c => c.Geometry.Count < 2))
        {
            throw new ArgumentException("At least one route candidate with geometry is required.", nameof(candidates));
        }

        var first = candidates.First(c => c.Geometry.Count >= 2);
        var graph = new MobilityGraph { SourceNodeId = 0, DestinationNodeId = 1 };
        graph.AddNode(new MobilityGraphNode { Id = 0, Coordinate = first.Geometry[0], CandidateOrder = 0 });
        graph.AddNode(new MobilityGraphNode { Id = 1, Coordinate = first.Geometry[^1], CandidateOrder = int.MaxValue });

        var nextNodeId = 2;
        var nextEdgeId = 1;

        foreach (var candidate in candidates.Where(c => c.Geometry.Count >= 2))
        {
            graph.SourceCandidates[candidate.CandidateKey] = candidate;
            var slices = SliceGeometry(candidate.Geometry, _options.GraphSampleMeters);
            if (slices.Count == 0)
            {
                continue;
            }

            var nodeIds = new List<int>(slices.Count + 1) { graph.SourceNodeId };
            for (var i = 0; i < slices.Count - 1; i++)
            {
                var node = new MobilityGraphNode
                {
                    Id = nextNodeId++,
                    Coordinate = slices[i].End,
                    CandidateKey = candidate.CandidateKey,
                    CandidateOrder = i + 1
                };
                graph.AddNode(node);
                nodeIds.Add(node.Id);
            }
            nodeIds.Add(graph.DestinationNodeId);

            var roadEdges = new List<RoadMobilityEdge>(slices.Count);
            for (var i = 0; i < slices.Count; i++)
            {
                var slice = slices[i];
                var midpoint = slice.Geometry[slice.Geometry.Count / 2];
                var match = _traffic.FindNearestRoad(midpoint.Latitude, midpoint.Longitude);
                var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    MobilityModes.Walk,
                    MobilityModes.Motorbike,
                    MobilityModes.Car
                };
                if (_rickshaw.IsEligible(match?.RoadClass))
                {
                    allowed.Add(MobilityModes.Rickshaw);
                }

                var edge = new RoadMobilityEdge
                {
                    Id = nextEdgeId++,
                    FromNodeId = nodeIds[i],
                    ToNodeId = nodeIds[i + 1],
                    CandidateKey = candidate.CandidateKey,
                    DistanceMeters = slice.DistanceMeters,
                    Geometry = slice.Geometry,
                    TrafficRoadId = match?.RoadId,
                    RoadName = match?.RoadName,
                    RoadClass = match?.RoadClass,
                    Area = match?.Area,
                    TrafficRoadMatchMeters = match?.DistanceMeters ?? double.PositiveInfinity,
                    Incidents = FilterIncidents(candidate.Incidents, slice.Geometry),
                    AllowedModes = allowed
                };
                graph.AddEdge(edge);
                roadEdges.Add(edge);
            }

            AddTransitStopMembership(graph, nodeIds, candidate.CandidateKey);
            AddBusEdges(graph, candidate, nodeIds, roadEdges, ref nextEdgeId);
        }

        return graph;
    }

    private void AddTransitStopMembership(MobilityGraph graph, IReadOnlyList<int> nodeIds, string candidateKey)
    {
        foreach (var stop in _transit.Stops)
        {
            var nearest = nodeIds
                .Select((nodeId, order) => new
                {
                    NodeId = nodeId,
                    Order = order,
                    Distance = GeoMath.HaversineDistanceKm(
                        stop.Latitude,
                        stop.Longitude,
                        graph.Nodes[nodeId].Coordinate.Latitude,
                        graph.Nodes[nodeId].Coordinate.Longitude) * 1000d
                })
                .OrderBy(x => x.Distance)
                .First();

            if (nearest.Distance <= _options.BusStopMatchMeters)
            {
                graph.Nodes[nearest.NodeId].TransitStopIds.Add(stop.StopId);
                if (!graph.Nodes[nearest.NodeId].TransitStopAccessMeters.TryGetValue(stop.StopId, out var existing) || nearest.Distance < existing)
                {
                    graph.Nodes[nearest.NodeId].TransitStopAccessMeters[stop.StopId] = nearest.Distance;
                }
            }
        }
    }

    private void AddBusEdges(
        MobilityGraph graph,
        RouteCandidateDto candidate,
        IReadOnlyList<int> nodeIds,
        IReadOnlyList<RoadMobilityEdge> roadEdges,
        ref int nextEdgeId)
    {
        foreach (var route in _transit.Routes)
        {
            var matches = new List<(string StopId, int NodeOrder, int NodeId)>();
            foreach (var stopId in route.StopIds)
            {
                var nodeMatch = nodeIds
                    .Select((nodeId, order) => new { NodeId = nodeId, Order = order, HasStop = graph.Nodes[nodeId].TransitStopIds.Contains(stopId) })
                    .Where(x => x.HasStop)
                    .OrderBy(x => x.Order)
                    .FirstOrDefault();
                if (nodeMatch is not null)
                {
                    matches.Add((stopId, nodeMatch.Order, nodeMatch.NodeId));
                }
            }

            for (var i = 0; i < matches.Count - 1; i++)
            {
                var routeA = matches[i];
                var routeB = matches[i + 1];
                if (routeA.NodeOrder == routeB.NodeOrder)
                {
                    continue;
                }

                // Synthetic development routes do not provide direction-specific trips. Treat
                // each corridor as bidirectional and orient the edge along this source→destination graph.
                var from = routeA.NodeOrder < routeB.NodeOrder ? routeA : routeB;
                var to = routeA.NodeOrder < routeB.NodeOrder ? routeB : routeA;
                var covered = roadEdges
                    .Skip(from.NodeOrder)
                    .Take(to.NodeOrder - from.NodeOrder)
                    .ToArray();
                if (covered.Length == 0)
                {
                    continue;
                }

                var geometry = JoinGeometry(covered.Select(e => e.Geometry));
                var distance = covered.Sum(e => e.DistanceMeters);
                var midpoint = geometry[geometry.Count / 2];
                var traffic = _traffic.FindNearestRoad(midpoint.Latitude, midpoint.Longitude);
                var incidents = FilterIncidents(candidate.Incidents, geometry);

                graph.AddEdge(new BusMobilityEdge
                {
                    Id = nextEdgeId++,
                    FromNodeId = from.NodeId,
                    ToNodeId = to.NodeId,
                    CandidateKey = candidate.CandidateKey,
                    DistanceMeters = distance,
                    Geometry = geometry,
                    TrafficRoadId = traffic?.RoadId,
                    RoadName = traffic?.RoadName,
                    RoadClass = traffic?.RoadClass,
                    Area = traffic?.Area,
                    TrafficRoadMatchMeters = traffic?.DistanceMeters ?? double.PositiveInfinity,
                    Incidents = incidents,
                    RouteId = route.RouteId,
                    RouteName = route.RouteName,
                    FromStopId = from.StopId,
                    ToStopId = to.StopId
                });
            }
        }
    }

    private static IReadOnlyList<RouteIncidentDto> FilterIncidents(
        IReadOnlyList<RouteIncidentDto> incidents,
        IReadOnlyList<RouteCoordinate> geometry) =>
        incidents.Where(i => RouteGeometryMath.DistancePointToPolylineMeters(
                new RouteCoordinate(i.Latitude, i.Longitude), geometry) <= 80d)
            .ToArray();

    private static IReadOnlyList<RouteCoordinate> JoinGeometry(IEnumerable<IReadOnlyList<RouteCoordinate>> parts)
    {
        var result = new List<RouteCoordinate>();
        foreach (var part in parts)
        {
            for (var i = 0; i < part.Count; i++)
            {
                if (result.Count > 0 && i == 0 && Same(result[^1], part[i]))
                {
                    continue;
                }
                result.Add(part[i]);
            }
        }
        return result;
    }

    private static List<GeometrySlice> SliceGeometry(IReadOnlyList<RouteCoordinate> geometry, double targetMeters)
    {
        var indices = new List<int> { 0 };
        var accumulated = 0d;
        for (var i = 1; i < geometry.Count; i++)
        {
            accumulated += SegmentDistance(geometry[i - 1], geometry[i]);
            if (accumulated >= targetMeters && i < geometry.Count - 1)
            {
                indices.Add(i);
                accumulated = 0;
            }
        }
        if (indices[^1] != geometry.Count - 1)
        {
            indices.Add(geometry.Count - 1);
        }

        var result = new List<GeometrySlice>();
        for (var i = 0; i < indices.Count - 1; i++)
        {
            var start = indices[i];
            var end = indices[i + 1];
            var points = geometry.Skip(start).Take(end - start + 1).ToArray();
            var distance = 0d;
            for (var p = 1; p < points.Length; p++)
            {
                distance += SegmentDistance(points[p - 1], points[p]);
            }
            if (distance > 0)
            {
                result.Add(new GeometrySlice(points[0], points[^1], points, distance));
            }
        }
        return result;
    }

    private static double SegmentDistance(RouteCoordinate a, RouteCoordinate b) =>
        GeoMath.HaversineDistanceKm(a.Latitude, a.Longitude, b.Latitude, b.Longitude) * 1000d;

    private static bool Same(RouteCoordinate a, RouteCoordinate b) =>
        Math.Abs(a.Latitude - b.Latitude) < 0.0000001 && Math.Abs(a.Longitude - b.Longitude) < 0.0000001;

    private sealed record GeometrySlice(
        RouteCoordinate Start,
        RouteCoordinate End,
        IReadOnlyList<RouteCoordinate> Geometry,
        double DistanceMeters);
}
