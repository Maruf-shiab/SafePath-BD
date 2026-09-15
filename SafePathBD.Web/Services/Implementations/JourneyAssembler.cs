using SafePathBD.Web.Common;
using SafePathBD.Web.Models.DTOs.IntelligentRouting;
using SafePathBD.Web.Models.DTOs.Routing;
using SafePathBD.Web.Models.DTOs.Traffic;
using SafePathBD.Web.Services.IntelligentRouting;
using SafePathBD.Web.Services.Interfaces;

namespace SafePathBD.Web.Services.Implementations;

public sealed class JourneyAssembler : IJourneyAssembler
{
    private readonly IRouteResilienceService _resilience;

    public JourneyAssembler(IRouteResilienceService resilience)
    {
        _resilience = resilience;
    }

    public AssembledJourney Assemble(
        JourneyPath path,
        MobilityGraph graph,
        IntelligentRouteRequest request,
        int viableCandidateCount)
    {
        var legs = new List<JourneyLegDto>();
        var edgeMap = new Dictionary<int, IReadOnlySet<int>>();
        var physicalSteps = path.Steps.Where(s => s.Edge is not null).ToArray();
        var totalPhysicalMinutes = physicalSteps.Sum(s => s.DurationMinutes);
        var totalDistance = physicalSteps.Sum(s => s.Edge!.DistanceMeters);
        var trafficWeighted = WeightedAverage(physicalSteps, s => s.Traffic?.PredictedCongestionIndex, s => s.DurationMinutes, 50d);
        var safetyWeighted = WeightedAverage(physicalSteps, s => s.Safety?.SafetyScore, s => s.DurationMinutes, 50d);
        var exposure = Exposure(physicalSteps);
        var dataConfidence = CalculateDataConfidence(physicalSteps, graph);
        var resilience = _resilience.Calculate(
            path,
            exposure,
            Math.Max(1d, path.Steps.Sum(s => s.DurationMinutes)),
            request.MaxTransfers,
            viableCandidateCount,
            request.DepartureTime ?? DateTimeOffset.Now);
        var incidents = physicalSteps
            .SelectMany(s => s.Edge!.Incidents)
            .GroupBy(i => i.ReportId)
            .Select(group => group.OrderByDescending(i => IncidentRank(i.IncidentLevel)).First())
            .ToArray();
        var incidentIds = incidents.Length;
        var affectedIncidentCount = incidents.Count(i => i.IncidentLevel == RouteIncidentStates.Affected);
        var cautionIncidentCount = incidents.Count(i => i.IncidentLevel == RouteIncidentStates.Caution);
        var incidentState = affectedIncidentCount > 0
            ? RouteIncidentStates.Affected
            : cautionIncidentCount > 0
                ? RouteIncidentStates.Caution
                : RouteIncidentStates.Clear;

        var sequence = 0;
        foreach (var step in path.Steps)
        {
            sequence++;
            var from = graph.Nodes[step.FromNodeId].Coordinate;
            var to = graph.Nodes[step.ToNodeId].Coordinate;
            if (step.Edge is null)
            {
                var busId = step.BusRouteId;
                legs.Add(new JourneyLegDto
                {
                    Sequence = sequence,
                    Mode = step.Mode,
                    Start = new JourneyPointDto(from.Latitude, from.Longitude),
                    End = new JourneyPointDto(to.Latitude, to.Longitude),
                    DistanceMeters = Math.Round(step.TransferWalkingMeters, 1),
                    DurationMinutes = Math.Round(step.DurationMinutes, 1),
                    ExpectedWaitMinutes = Math.Round(step.ExpectedWaitMinutes, 1),
                    TransferNote = step.TransferNote,
                    BusRouteId = busId,
                    BusRoute = step.BusRouteName,
                    IsTransfer = true,
                    TrafficLevel = "N/A",
                    TrafficConfidence = "N/A",
                    SafetySource = "N/A",
                    RiskClass = "N/A",
                    HazardState = RouteIncidentStates.Clear,
                    DataConfidence = "N/A"
                });
                edgeMap[sequence] = new HashSet<int>();
                continue;
            }

            var edge = step.Edge;
            var confidence = StepConfidence(step);
            var bus = edge as BusMobilityEdge;
            legs.Add(new JourneyLegDto
            {
                Sequence = sequence,
                Mode = step.Mode,
                Start = new JourneyPointDto(from.Latitude, from.Longitude),
                End = new JourneyPointDto(to.Latitude, to.Longitude),
                DistanceMeters = Math.Round(edge.DistanceMeters, 1),
                DurationMinutes = Math.Round(step.DurationMinutes, 1),
                PredictedCongestionIndex = Math.Round(step.Traffic?.PredictedCongestionIndex ?? 0d, 1),
                TrafficLevel = step.Traffic?.TrafficLevel ?? TrafficLevels.Moderate,
                TrafficConfidence = step.Traffic?.TrafficDataConfidence ?? DataConfidenceLevels.Low,
                SafetyScore = step.Safety?.SafetyScore,
                SafetySource = step.Safety?.Source ?? "UNKNOWN",
                RiskClass = step.Safety?.RiskClass ?? "UNKNOWN",
                HazardState = step.Safety?.HazardState ?? RouteIncidentStates.Unknown,
                Geometry = edge.Geometry,
                TrafficRoadId = edge.TrafficRoadId,
                RoadName = edge.RoadName,
                RoadClass = edge.RoadClass,
                BusRouteId = bus?.RouteId ?? step.BusRouteId,
                BusRoute = bus?.RouteName ?? step.BusRouteName,
                ExpectedWaitMinutes = Math.Round(step.ExpectedWaitMinutes, 1),
                TransferNote = step.TransferNote,
                IsTransfer = false,
                DataConfidence = confidence
            });
            edgeMap[sequence] = new HashSet<int> { edge.Id };
        }

        var modeSequence = BuildModeSequence(path.Steps);
        var transferConvenience = 100d - Math.Min(100d, path.TransferCount / (double)Math.Max(1, request.MaxTransfers) * 100d);
        var walkingConvenience = 100d - Math.Min(100d, path.WalkingMeters / Math.Max(1d, request.MaxWalkingMeters) * 100d);
        var dto = new JourneyOptionDto
        {
            Id = path.Id,
            TotalDistanceMeters = Math.Round(totalDistance, 1),
            TotalDurationMinutes = Math.Round(path.Steps.Sum(s => s.DurationMinutes), 1),
            Modes = modeSequence,
            TransferCount = path.TransferCount,
            WalkingDistanceMeters = Math.Round(path.WalkingMeters, 1),
            PredictedCongestionIndex = Math.Round(trafficWeighted, 1),
            TrafficLevel = TrafficLevels.FromIndex(trafficWeighted),
            SafetyScore = Math.Round(safetyWeighted, 1),
            SafetyExposure = exposure,
            DataConfidence = Math.Round(dataConfidence, 1),
            Resilience = resilience,
            HazardCount = incidentIds,
            IncidentState = incidentState,
            AffectedIncidentCount = affectedIncidentCount,
            CautionIncidentCount = cautionIncidentCount,
            Legs = legs,
            JourneyDna = new JourneyDnaDto(
                Math.Round(safetyWeighted, 1),
                Math.Round(Math.Clamp(100d - trafficWeighted, 0d, 100d), 1),
                Math.Round(transferConvenience, 1),
                Math.Round(walkingConvenience, 1),
                resilience,
                Math.Round(dataConfidence, 1)),
            SpatialSignatureLengthMeters = Math.Round(totalDistance, 1)
        };

        return new AssembledJourney { Dto = dto, Path = path, LegEdgeIds = edgeMap };
    }


    private static IReadOnlyList<string> BuildModeSequence(IReadOnlyList<JourneyPathStep> steps)
    {
        var modes = new List<string>();
        foreach (var step in steps)
        {
            // Access walking is represented on transfer steps rather than road edges. Surface it
            // in the visible mode sequence so Bus/Rickshaw transfers do not hide real walking.
            if (step.Edge is null && step.TransferWalkingMeters > 0.5d)
            {
                modes.Add(MobilityModes.Walk);
                modes.Add(step.Mode);
                continue;
            }

            if (step.Edge is not null)
            {
                modes.Add(step.Mode);
            }
        }

        return modes.DistinctAdjacent().ToArray();
    }

    private static int IncidentRank(string? state) => state switch
    {
        RouteIncidentStates.Affected => 2,
        RouteIncidentStates.Caution => 1,
        _ => 0
    };

    private static SafetyExposureDto Exposure(IEnumerable<JourneyPathStep> steps)
    {
        var veryHigh = 0d;
        var high = 0d;
        var moderate = 0d;
        var lower = 0d;
        foreach (var step in steps)
        {
            switch (step.Safety?.RiskClass)
            {
                case "VERY_HIGH": veryHigh += step.DurationMinutes; break;
                case "HIGH": high += step.DurationMinutes; break;
                case "MODERATE": moderate += step.DurationMinutes; break;
                default: lower += step.DurationMinutes; break;
            }
        }
        return new SafetyExposureDto(
            Math.Round(veryHigh, 1),
            Math.Round(high, 1),
            Math.Round(moderate, 1),
            Math.Round(lower, 1));
    }

    private static double CalculateDataConfidence(IReadOnlyList<JourneyPathStep> steps, MobilityGraph graph)
    {
        if (steps.Count == 0) return 0;
        var traffic = steps.Select(s => ConfidenceNumber(s.Traffic?.TrafficDataConfidence)).Average();
        var matching = steps.Select(s => MatchConfidence(s.Edge!.TrafficRoadMatchMeters)).Average();
        var safety = steps.Select(s => s.Safety?.Coverage ?? 0.2d).Average();
        var incident = steps.Select(s => graph.SourceCandidates.TryGetValue(s.Edge!.CandidateKey, out var candidate) && candidate.IncidentCheckAvailable ? 1d : 0.5d).Average();
        return Math.Clamp((traffic * 0.45d + matching * 0.25d + safety * 0.20d + incident * 0.10d) * 100d, 0d, 100d);
    }

    private static string StepConfidence(JourneyPathStep step)
    {
        var score = ConfidenceNumber(step.Traffic?.TrafficDataConfidence) * 0.55d
            + MatchConfidence(step.Edge!.TrafficRoadMatchMeters) * 0.25d
            + (step.Safety?.Coverage ?? 0.2d) * 0.20d;
        return score >= 0.8 ? DataConfidenceLevels.High : score >= 0.55 ? DataConfidenceLevels.Medium : DataConfidenceLevels.Low;
    }

    private static double ConfidenceNumber(string? value) => value switch
    {
        DataConfidenceLevels.High => 0.95d,
        DataConfidenceLevels.Medium => 0.70d,
        _ => 0.45d
    };

    private static double MatchConfidence(double meters) => meters switch
    {
        <= 250 => 0.95,
        <= 650 => 0.75,
        <= 1200 => 0.50,
        _ => 0.25
    };

    private static double WeightedAverage(
        IEnumerable<JourneyPathStep> steps,
        Func<JourneyPathStep, double?> selector,
        Func<JourneyPathStep, double> weight,
        double fallback)
    {
        var values = steps.Select(s => new { Value = selector(s), Weight = Math.Max(0.01, weight(s)) })
            .Where(x => x.Value.HasValue)
            .ToArray();
        if (values.Length == 0) return fallback;
        return values.Sum(x => x.Value!.Value * x.Weight) / values.Sum(x => x.Weight);
    }

}

file static class EnumerableJourneyExtensions
{
    public static IEnumerable<T> DistinctAdjacent<T>(this IEnumerable<T> source)
    {
        var comparer = EqualityComparer<T>.Default;
        var first = true;
        T? previous = default;
        foreach (var item in source)
        {
            if (first || !comparer.Equals(previous!, item))
            {
                yield return item;
                previous = item;
                first = false;
            }
        }
    }
}
