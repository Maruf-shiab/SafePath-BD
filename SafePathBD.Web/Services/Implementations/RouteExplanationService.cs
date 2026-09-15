using SafePathBD.Web.Models.DTOs.IntelligentRouting;
using SafePathBD.Web.Services.Interfaces;

namespace SafePathBD.Web.Services.Implementations;

public sealed class RouteExplanationService : IRouteExplanationService
{
    public string ExplainRecommended(JourneyOptionDto option, IReadOnlyList<JourneyOptionDto> candidatePool)
    {
        var fastest = candidatePool.OrderBy(x => x.TotalDurationMinutes).FirstOrDefault();
        var reasons = new List<string>();

        if (option.HazardCount == 0)
        {
            reasons.Add("no critical verified road hazard is carried by this journey");
        }
        else
        {
            reasons.Add($"it limits the journey to {option.HazardCount} verified hazard/incident signal(s)");
        }

        if (option.SafetyExposure.ElevatedRiskMinutes <= 3)
        {
            reasons.Add($"elevated-risk exposure is about {option.SafetyExposure.ElevatedRiskMinutes:0.#} min");
        }

        if (option.PredictedCongestionIndex < 55)
        {
            reasons.Add($"predicted congestion is {option.TrafficLevel.ToLowerInvariant()}");
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
            reasons.Add("it has the lowest configured balance of travel time, safety signal, congestion, walking, transfers, distance and resilience");
        }

        return "This journey is recommended because " + string.Join(", ", reasons) + ".";
    }

    public IReadOnlyList<JourneyTradeoffDto> ExplainTradeoffs(JourneyOptionDto option, JourneyOptionDto benchmark)
    {
        var items = new List<JourneyTradeoffDto>();
        if (option.Id == benchmark.Id) return items;

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
}
