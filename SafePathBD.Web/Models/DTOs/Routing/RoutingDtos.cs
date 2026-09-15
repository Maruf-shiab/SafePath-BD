namespace SafePathBD.Web.Models.DTOs.Routing;

public static class RouteIncidentStates
{
    public const string Clear = "CLEAR";
    public const string Caution = "CAUTION";
    public const string Affected = "AFFECTED";
    public const string Unknown = "UNKNOWN";
}

public sealed record RoutePointRequest(double Latitude, double Longitude, string? Label = null);

public sealed record RouteSearchRequest(RoutePointRequest Start, RoutePointRequest Destination);

public sealed record RouteCoordinate(double Latitude, double Longitude);

/// <summary>
/// Public routing-safe incident projection. It deliberately excludes reporter identity,
/// contact data, votes, moderator notes and evidence URLs.
/// </summary>
public sealed record RouteIncidentDto(
    ulong ReportId,
    string ReportType,
    string Title,
    double Latitude,
    double Longitude,
    string? LocationLabel,
    string? AccidentType,
    string? SeverityName,
    string? RiskLevel,
    string? HazardType,
    string IncidentLevel,
    double DistanceFromRouteMeters,
    DateTime ReportedAt);

public sealed class RouteCandidateDto
{
    public string CandidateKey { get; init; } = string.Empty;
    public int ProviderIndex { get; init; }
    public double DistanceMeters { get; init; }
    public double DistanceKm => Math.Round(DistanceMeters / 1000d, 2);
    public double DurationSeconds { get; init; }
    public double DurationMinutes => Math.Round(DurationSeconds / 60d, 1);
    public IReadOnlyList<RouteCoordinate> Geometry { get; init; } = Array.Empty<RouteCoordinate>();
    public bool IsShortest { get; set; }
    public bool IsFastest { get; set; }
    public bool TrafficEstimateAvailable { get; set; }
    public double? PredictedCongestionIndex { get; set; }
    public string? TrafficLevel { get; set; }
    public string? TrafficDataConfidence { get; set; }
    public double? TrafficAdjustedDurationSeconds { get; set; }
    public double? TrafficAdjustedDurationMinutes => TrafficAdjustedDurationSeconds is null
        ? null
        : Math.Round(TrafficAdjustedDurationSeconds.Value / 60d, 1);
    public double EffectiveDurationSeconds => TrafficAdjustedDurationSeconds ?? DurationSeconds;
    public string IncidentState { get; set; } = RouteIncidentStates.Unknown;
    public int IncidentCount => Incidents.Count;
    public bool IncidentCheckAvailable { get; set; } = true;
    public bool IsRecommendedAlternative { get; set; }
    public bool IsDetour { get; init; }
    public IReadOnlyList<RouteIncidentDto> Incidents { get; set; } = Array.Empty<RouteIncidentDto>();
}

public sealed class RouteSearchResponseDto
{
    public Guid SearchId { get; init; }
    public IReadOnlyList<RouteCandidateDto> Candidates { get; init; } = Array.Empty<RouteCandidateDto>();
    public string? ShortestCandidateKey { get; init; }
    public string? FastestCandidateKey { get; init; }
    public string? RecommendedCandidateKey { get; init; }
    public DateTime IncidentCheckedAt { get; init; }
    public bool IncidentCheckAvailable { get; init; }
    public string? IncidentMessage { get; init; }
    public bool HasAffectedRoute { get; init; }
    public bool DetourAttempted { get; init; }
    public string? DetourMessage { get; init; }
    public double? RecommendedDistanceDeltaKm { get; init; }
    public double? RecommendedDurationDeltaMinutes { get; init; }
    public string? RecommendationReason { get; init; }
    public bool TrafficAwareRecommendation { get; init; }
    public DateTime? TrafficEstimatedAt { get; init; }
    public int IncidentRecheckSeconds { get; init; }
}

public sealed record RouteRecheckRequest(Guid SearchId, string SelectedCandidateKey);

public sealed class RouteRecheckResponseDto
{
    public Guid SearchId { get; init; }
    public string CandidateKey { get; init; } = string.Empty;
    public string PreviousIncidentState { get; init; } = RouteIncidentStates.Unknown;
    public string IncidentState { get; init; } = RouteIncidentStates.Unknown;
    public IReadOnlyList<RouteIncidentDto> Incidents { get; init; } = Array.Empty<RouteIncidentDto>();
    public DateTime IncidentCheckedAt { get; init; }
    public bool IncidentChanged { get; init; }
    public bool RerouteRecommended { get; init; }
    public bool IncidentCheckAvailable { get; init; }
    public string? Message { get; init; }
}
