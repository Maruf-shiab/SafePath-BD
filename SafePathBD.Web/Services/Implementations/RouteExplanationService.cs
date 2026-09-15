using SafePathBD.Web.Models.DTOs.IntelligentRouting;
using SafePathBD.Web.Models.DTOs.Routing;
using SafePathBD.Web.Services.Interfaces;

namespace SafePathBD.Web.Services.Implementations;

public sealed class RouteExplanationService : IRouteExplanationService
{
    public string ExplainRecommended(JourneyOptionDto option, IReadOnlyList<JourneyOptionDto> candidatePool)
    {
        var fastest = candidatePool.OrderBy(x => x.TotalDurationMinutes).FirstOrDefault();
        var reasons = new List<string>();

        if (option.IncidentState == RouteIncidentStates.Clear)
        {
            reasons.Add("no active verified accident or serious hazard was detected on this option");
        }
        else if (option.IncidentState == RouteIncidentStates.Caution)
        {
            reasons.Add($"it has only caution-level verified incident information ({option.CautionIncidentCount} signal(s))");
        }
        else
        {
            reasons.Add($"all available practical alternatives still contain an affected verified incident ({option.AffectedIncidentCount} signal(s))");
        }

        if (option.SafetyExposure.ElevatedRiskMinutes <= 3)
        {
            reasons.Add($"elevated-risk exposure is about {option.SafetyExposure.ElevatedRiskMinutes:0.#} min");
        }

        if (option.PredictedCongestionIndex < 55)
        {
            reasons.Add($"predicted congestion is {option.TrafficLevel.ToLowerInvariant()}");
        }
        else
        {
            reasons.Add($"its predicted congestion is {option.TrafficLevel.ToLowerInvariant()} ({option.PredictedCongestionIndex:0}/100)");
        }

        if (option.TransferCount <= 1)
        {
            reasons.Add($"it uses only {option.TransferCount} transfer{(option.TransferCount == 1 ? "" : "s")}");
        }

        if (fastest is not null && fastest.Id != option.Id)
        {
            var slower = option.TotalDurationMinutes - fastest.TotalDurationMinutes;
            if (slower > 0 && slower <= 8)
            {
                reasons.Add($"it is only about {slower:0.#} min slower than the fastest practical option");
            }
        }

        if (reasons.Count == 0)
        {
            reasons.Add("it has the lowest configured balance of travel time, verified incident state, predicted congestion, walking, transfers, distance and resilience");
        }

        return "This journey is recommended because " + string.Join(", ", reasons) + ".";
    }

    public IReadOnlyList<JourneyTradeoffDto> ExplainTradeoffs(JourneyOptionDto option, JourneyOptionDto benchmark)
    {
        var items = new List<JourneyTradeoffDto>();
        if (option.Id == benchmark.Id)
        {
            return items;
        }

        if (!string.Equals(option.IncidentState, benchmark.IncidentState, StringComparison.OrdinalIgnoreCase))
        {
            items.Add(new JourneyTradeoffDto(
                "Verified incidents",
                $"This option is {FriendlyIncident(option.IncidentState)}, while the recommended option is {FriendlyIncident(benchmark.IncidentState)}.",
                IncidentRank(option.IncidentState) < IncidentRank(benchmark.IncidentState) ? "positive" : "warning"));
        }

        var time = option.TotalDurationMinutes - benchmark.TotalDurationMinutes;
        if (Math.Abs(time) >= 0.5)
        {
            items.Add(new JourneyTradeoffDto(
                "Travel time",
                time < 0 ? $"About {Math.Abs(time):0.#} min faster." : $"About {time:0.#} min slower.",
                time < 0 ? "positive" : "warning"));
        }

        var congestion = option.PredictedCongestionIndex - benchmark.PredictedCongestionIndex;
        if (Math.Abs(congestion) >= 3)
        {
            items.Add(new JourneyTradeoffDto(
                "Predicted traffic",
                congestion < 0 ? $"Congestion index is about {Math.Abs(congestion):0} points lower." : $"Congestion index is about {congestion:0} points higher.",
                congestion < 0 ? "positive" : "warning"));
        }

        var exposure = option.SafetyExposure.ElevatedRiskMinutes - benchmark.SafetyExposure.ElevatedRiskMinutes;
        if (Math.Abs(exposure) >= 0.5)
        {
            items.Add(new JourneyTradeoffDto(
                "High-risk exposure",
                exposure < 0 ? $"About {Math.Abs(exposure):0.#} fewer elevated-risk minutes." : $"About {exposure:0.#} more elevated-risk minutes.",
                exposure < 0 ? "positive" : "warning"));
        }

        if (option.TransferCount != benchmark.TransferCount)
        {
            var delta = option.TransferCount - benchmark.TransferCount;
            items.Add(new JourneyTradeoffDto(
                "Transfers",
                delta < 0 ? $"{Math.Abs(delta)} fewer transfer(s)." : $"{delta} additional transfer(s).",
                delta < 0 ? "positive" : "warning"));
        }

        if (!string.Equals(ModeSignature(option), ModeSignature(benchmark), StringComparison.OrdinalIgnoreCase))
        {
            items.Add(new JourneyTradeoffDto(
                "Travel modes",
                $"Uses {ReadableModes(option)} instead of {ReadableModes(benchmark)}.",
                "neutral"));
        }

        var resilience = option.Resilience - benchmark.Resilience;
        if (Math.Abs(resilience) >= 3)
        {
            items.Add(new JourneyTradeoffDto(
                "Resilience",
                resilience > 0 ? $"Resilience is {resilience:0} points higher." : $"Resilience is {Math.Abs(resilience):0} points lower.",
                resilience > 0 ? "positive" : "warning"));
        }

        return items;
    }

    private static int IncidentRank(string? state) => state switch
    {
        RouteIncidentStates.Affected => 2,
        RouteIncidentStates.Caution => 1,
        _ => 0
    };

    private static string FriendlyIncident(string? state) => state switch
    {
        RouteIncidentStates.Affected => "affected by a serious verified incident",
        RouteIncidentStates.Caution => "under caution from a verified incident",
        _ => "clear of currently detected active verified incidents"
    };

    private static string ModeSignature(JourneyOptionDto option) =>
        string.Join(">", option.Modes.Select(m => m.Trim().ToUpperInvariant()));

    private static string ReadableModes(JourneyOptionDto option) =>
        string.Join(" → ", option.Modes.Select(m => m.ToLowerInvariant()));
}
