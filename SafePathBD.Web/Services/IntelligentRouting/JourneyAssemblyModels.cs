using SafePathBD.Web.Models.DTOs.IntelligentRouting;

namespace SafePathBD.Web.Services.IntelligentRouting;

public sealed class AssembledJourney
{
    public JourneyOptionDto Dto { get; init; } = new();
    public JourneyPath Path { get; init; } = new();
    public IReadOnlyDictionary<int, IReadOnlySet<int>> LegEdgeIds { get; init; } = new Dictionary<int, IReadOnlySet<int>>();
}

public sealed class IntelligentSearchCacheEntry
{
    public ulong? UserId { get; init; }
    public IntelligentRouteRequest Request { get; init; } = new();
    public MobilityGraph Graph { get; init; } = new();
    public IReadOnlyList<AssembledJourney> CandidatePool { get; init; } = Array.Empty<AssembledJourney>();
}
