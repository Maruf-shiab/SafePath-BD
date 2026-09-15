using SafePathBD.Web.Models.DTOs.Routing;
using SafePathBD.Web.Services.IntelligentRouting;

namespace SafePathBD.Web.Services.Interfaces;

public interface IMultimodalGraphBuilder
{
    MobilityGraph Build(IReadOnlyList<RouteCandidateDto> candidates);
}
