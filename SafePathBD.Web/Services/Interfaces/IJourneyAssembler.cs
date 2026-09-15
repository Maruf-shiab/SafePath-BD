using SafePathBD.Web.Models.DTOs.IntelligentRouting;
using SafePathBD.Web.Services.IntelligentRouting;

namespace SafePathBD.Web.Services.Interfaces;

public interface IJourneyAssembler
{
    AssembledJourney Assemble(
        JourneyPath path,
        MobilityGraph graph,
        IntelligentRouteRequest request,
        int viableCandidateCount);
}
