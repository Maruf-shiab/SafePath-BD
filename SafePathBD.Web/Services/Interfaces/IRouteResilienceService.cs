using SafePathBD.Web.Models.DTOs.IntelligentRouting;
using SafePathBD.Web.Services.IntelligentRouting;

namespace SafePathBD.Web.Services.Interfaces;

public interface IRouteResilienceService
{
    double Calculate(
        JourneyPath path,
        SafetyExposureDto exposure,
        double totalDurationMinutes,
        int maxTransfers,
        int viableCandidateCount,
        DateTimeOffset departureTime);
}

public interface IRouteExplanationService
{
    string ExplainRecommended(JourneyOptionDto option, IReadOnlyList<JourneyOptionDto> candidatePool);
    IReadOnlyList<JourneyTradeoffDto> ExplainTradeoffs(JourneyOptionDto option, JourneyOptionDto benchmark);
}
