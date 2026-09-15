using SafePathBD.Web.Models.DTOs.IntelligentRouting;
using SafePathBD.Web.Services.IntelligentRouting;

namespace SafePathBD.Web.Services.Interfaces;

public interface ITimeDependentJourneyRouter
{
    Task<JourneyPath?> FindBestAsync(
        MobilityGraph graph,
        IntelligentRouteRequest request,
        IReadOnlySet<int>? excludedEdgeIds = null,
        string? singleModeOnly = null,
        CancellationToken cancellationToken = default);
}

public interface IKBestJourneyPlanner
{
    Task<IReadOnlyList<JourneyPath>> FindCandidatesAsync(
        MobilityGraph graph,
        IntelligentRouteRequest request,
        IReadOnlySet<int>? globallyBlockedEdgeIds = null,
        CancellationToken cancellationToken = default);
}
