using SafePathBD.Web.Models.DTOs.Routing;

namespace SafePathBD.Web.Models.DTOs.IntelligentRouting;

public sealed class IntelligentRouteRequest
{
    public RoutePointRequest Start { get; set; } = new(0, 0);
    public RoutePointRequest Destination { get; set; } = new(0, 0);
    public DateTimeOffset? DepartureTime { get; set; }
    public List<string> EnabledModes { get; set; } = new();
    public string Preference { get; set; } = "BALANCED";
    public int MaxWalkingMeters { get; set; } = 1000;
    public int MaxTransfers { get; set; } = 2;
}

public sealed record JourneyPointDto(double Latitude, double Longitude, string? Label = null);

public sealed class JourneyLegDto
{
    public int Sequence { get; init; }
    public string Mode { get; init; } = string.Empty;
    public JourneyPointDto Start { get; init; } = new(0, 0);
    public JourneyPointDto End { get; init; } = new(0, 0);
    public double DistanceMeters { get; init; }
    public double DistanceKm => Math.Round(DistanceMeters / 1000d, 2);
    public double DurationMinutes { get; init; }
    public double PredictedCongestionIndex { get; init; }
    public string TrafficLevel { get; init; } = string.Empty;
    public string TrafficConfidence { get; init; } = string.Empty;
    public double? SafetyScore { get; init; }
    public string SafetySource { get; init; } = string.Empty;
    public string RiskClass { get; init; } = string.Empty;
    public string HazardState { get; init; } = string.Empty;
    public IReadOnlyList<RouteCoordinate> Geometry { get; init; } = Array.Empty<RouteCoordinate>();
    public string? TrafficRoadId { get; init; }
    public string? RoadName { get; init; }
    public string? RoadClass { get; init; }
    public string? BusRouteId { get; init; }
    public string? BusRoute { get; init; }
    public double ExpectedWaitMinutes { get; init; }
    public string? TransferNote { get; init; }
    public bool IsTransfer { get; init; }
    public string DataConfidence { get; init; } = string.Empty;
}

public sealed record SafetyExposureDto(
    double VeryHighRiskMinutes,
    double HighRiskMinutes,
    double ModerateRiskMinutes,
    double LowerRiskMinutes)
{
    public double ElevatedRiskMinutes => Math.Round(VeryHighRiskMinutes + HighRiskMinutes, 1);
}

public sealed record JourneyDnaDto(
    double Safety,
    double TrafficResilience,
    double TransferConvenience,
    double WalkingBurden,
    double RouteResilience,
    double DataConfidence);

public sealed record JourneyTradeoffDto(string Title, string Detail, string Tone = "neutral");

public sealed class JourneyOptionDto
{
    public string Id { get; init; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public double TotalDistanceMeters { get; init; }
    public double TotalDistanceKm => Math.Round(TotalDistanceMeters / 1000d, 2);
    public double TotalDurationMinutes { get; init; }
    public IReadOnlyList<string> Modes { get; init; } = Array.Empty<string>();
    public int TransferCount { get; init; }
    public double WalkingDistanceMeters { get; init; }
    public double WalkingDistanceKm => Math.Round(WalkingDistanceMeters / 1000d, 2);
    public double PredictedCongestionIndex { get; init; }
    public string TrafficLevel { get; init; } = string.Empty;
    public double? SafetyScore { get; init; }
    public SafetyExposureDto SafetyExposure { get; init; } = new(0, 0, 0, 0);
    public double DataConfidence { get; init; }
    public double Resilience { get; init; }
    public int HazardCount { get; init; }
    public string IncidentState { get; init; } = RouteIncidentStates.Clear;
    public int AffectedIncidentCount { get; init; }
    public int CautionIncidentCount { get; init; }
    public double GeneralizedCost { get; set; }
    public IReadOnlyList<JourneyLegDto> Legs { get; init; } = Array.Empty<JourneyLegDto>();
    public string Explanation { get; set; } = string.Empty;
    public IReadOnlyList<JourneyTradeoffDto> Tradeoffs { get; set; } = Array.Empty<JourneyTradeoffDto>();
    public JourneyDnaDto JourneyDna { get; init; } = new(0, 0, 0, 0, 0, 0);
    public double SpatialSignatureLengthMeters { get; init; }
}

public sealed record DepartureWindowOptionDto(
    DateTimeOffset DepartureTime,
    double EstimatedDurationMinutes,
    double PredictedCongestionIndex,
    double DifferenceMinutes);

public sealed class DepartureWindowDto
{
    public IReadOnlyList<DepartureWindowOptionDto> Options { get; init; } = Array.Empty<DepartureWindowOptionDto>();
    public bool HasSuggestion { get; init; }
    public string? Suggestion { get; init; }
    public DateTimeOffset? SuggestedDepartureTime { get; init; }
}

public sealed class IntelligentRouteResponseDto
{
    public Guid SearchId { get; init; }
    public DateTimeOffset DepartureTime { get; init; }
    public string Preference { get; init; } = string.Empty;
    public IReadOnlyList<JourneyOptionDto> Journeys { get; init; } = Array.Empty<JourneyOptionDto>();
    public string? BestOverallJourneyId { get; init; }
    public string? FastestPracticalJourneyId { get; init; }
    public string? ResilientJourneyId { get; init; }
    public JourneyOptionDto? BackupJourney { get; init; }
    public DepartureWindowDto DepartureWindow { get; init; } = new();
    public string TrafficModelName { get; init; } = string.Empty;
    public string TrafficModelVersion { get; init; } = string.Empty;
    public string TrafficDataStatus { get; init; } = "SYNTHETIC_DEVELOPMENT";
    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
    public DateTime GeneratedAtUtc { get; init; }
}

public sealed record IntelligentRouteSearchResult(
    string Status,
    string? Message,
    IntelligentRouteResponseDto? Response);

public sealed class WhatIfDisruptionRequest
{
    public Guid SearchId { get; set; }
    public string JourneyId { get; set; } = string.Empty;
    public int LegSequence { get; set; }
}

public sealed class WhatIfDisruptionResponseDto
{
    public Guid SearchId { get; init; }
    public bool IsSimulation { get; init; } = true;
    public string Scenario { get; init; } = string.Empty;
    public IReadOnlyList<JourneyOptionDto> Journeys { get; init; } = Array.Empty<JourneyOptionDto>();
    public string? RecommendedJourneyId { get; init; }
    public string? Message { get; init; }
}
