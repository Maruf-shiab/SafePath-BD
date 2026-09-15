using SafePathBD.Web.Models.DTOs.Routing;
using SafePathBD.Web.Models.DTOs.Traffic;
using SafePathBD.Web.Services.Interfaces;

namespace SafePathBD.Web.Services.IntelligentRouting;

public sealed class MobilityGraph
{
    public int SourceNodeId { get; init; }
    public int DestinationNodeId { get; init; }
    public Dictionary<int, MobilityGraphNode> Nodes { get; } = new();
    public Dictionary<int, List<MobilityEdge>> Outgoing { get; } = new();
    public Dictionary<string, RouteCandidateDto> SourceCandidates { get; } = new(StringComparer.OrdinalIgnoreCase);

    public IEnumerable<MobilityEdge> Edges => Outgoing.Values.SelectMany(x => x);

    public void AddNode(MobilityGraphNode node)
    {
        Nodes[node.Id] = node;
        Outgoing.TryAdd(node.Id, new List<MobilityEdge>());
    }

    public void AddEdge(MobilityEdge edge)
    {
        if (!Outgoing.TryGetValue(edge.FromNodeId, out var list))
        {
            list = new List<MobilityEdge>();
            Outgoing[edge.FromNodeId] = list;
        }
        list.Add(edge);
    }
}

public sealed class MobilityGraphNode
{
    public int Id { get; init; }
    public RouteCoordinate Coordinate { get; init; } = new(0, 0);
    public string? CandidateKey { get; init; }
    public int CandidateOrder { get; init; }
    public HashSet<string> TransitStopIds { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, double> TransitStopAccessMeters { get; } = new(StringComparer.OrdinalIgnoreCase);
}

public abstract class MobilityEdge
{
    public int Id { get; init; }
    public int FromNodeId { get; init; }
    public int ToNodeId { get; init; }
    public string CandidateKey { get; init; } = string.Empty;
    public double DistanceMeters { get; init; }
    public IReadOnlyList<RouteCoordinate> Geometry { get; init; } = Array.Empty<RouteCoordinate>();
    public string? TrafficRoadId { get; init; }
    public string? RoadName { get; init; }
    public string? RoadClass { get; init; }
    public string? Area { get; init; }
    public double TrafficRoadMatchMeters { get; init; }
    public IReadOnlyList<RouteIncidentDto> Incidents { get; init; } = Array.Empty<RouteIncidentDto>();
    public abstract string Kind { get; }
}

public sealed class RoadMobilityEdge : MobilityEdge
{
    public override string Kind => "ROAD";
    public HashSet<string> AllowedModes { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class BusMobilityEdge : MobilityEdge
{
    public override string Kind => "BUS";
    public string RouteId { get; init; } = string.Empty;
    public string RouteName { get; init; } = string.Empty;
    public string FromStopId { get; init; } = string.Empty;
    public string ToStopId { get; init; } = string.Empty;
}

public sealed record TraversalEvaluation(
    MobilityEdge Edge,
    string Mode,
    double DurationMinutes,
    double ExpectedWaitMinutes,
    TrafficPredictionResult Traffic,
    SegmentSafetySnapshot Safety,
    double GeneralizedEdgeCost,
    string? TransferNote = null,
    string? BusRouteName = null);

public sealed record JourneyPathStep(
    int Sequence,
    int FromNodeId,
    int ToNodeId,
    string Mode,
    MobilityEdge? Edge,
    double DurationMinutes,
    double ExpectedWaitMinutes,
    TrafficPredictionResult? Traffic,
    SegmentSafetySnapshot? Safety,
    string? TransferNote,
    string? BusRouteId,
    string? BusRouteName,
    double TransferWalkingMeters = 0);

public sealed class JourneyPath
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public IReadOnlyList<JourneyPathStep> Steps { get; init; } = Array.Empty<JourneyPathStep>();
    public double GeneralizedSearchCost { get; init; }
    public DateTimeOffset ArrivalTime { get; init; }
    public int TransferCount { get; init; }
    public double WalkingMeters { get; init; }

    public IReadOnlySet<int> PhysicalEdgeIds => Steps
        .Where(s => s.Edge is not null)
        .Select(s => s.Edge!.Id)
        .ToHashSet();
}
