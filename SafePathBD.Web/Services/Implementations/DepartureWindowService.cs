using Microsoft.Extensions.Options;
using SafePathBD.Web.Common;
using SafePathBD.Web.Models.DTOs.IntelligentRouting;
using SafePathBD.Web.Models.DTOs.Traffic;
using SafePathBD.Web.Services.Interfaces;

namespace SafePathBD.Web.Services.Implementations;

public sealed class DepartureWindowService : IDepartureWindowService
{
    private static readonly int[] Offsets = [0, 15, 30, 60];
    private readonly IVehicleTravelTimeService _travelTime;
    private readonly IntelligentMobilityOptions _options;

    public DepartureWindowService(
        IVehicleTravelTimeService travelTime,
        IOptions<IntelligentMobilityOptions> options)
    {
        _travelTime = travelTime;
        _options = options.Value;
    }

    public async Task<DepartureWindowDto> EvaluateAsync(
        JourneyOptionDto journey,
        IntelligentRouteRequest request,
        CancellationToken cancellationToken = default)
    {
        var baseDeparture = request.DepartureTime ?? DateTimeOffset.Now;
        var results = new List<DepartureWindowOptionDto>();

        foreach (var offset in Offsets)
        {
            var departure = baseDeparture.AddMinutes(offset);
            var clock = departure;
            var total = 0d;
            var congestionWeighted = 0d;
            var trafficMinutes = 0d;

            foreach (var leg in journey.Legs)
            {
                if (leg.IsTransfer)
                {
                    total += leg.DurationMinutes;
                    clock = clock.AddMinutes(leg.DurationMinutes);
                    continue;
                }

                var midpoint = leg.Geometry.Count > 0
                    ? leg.Geometry[leg.Geometry.Count / 2]
                    : new SafePathBD.Web.Models.DTOs.Routing.RouteCoordinate(leg.Start.Latitude, leg.Start.Longitude);
                var predictionRequest = new TrafficPredictionRequest(
                    leg.TrafficRoadId,
                    midpoint.Latitude,
                    midpoint.Longitude,
                    clock,
                    _options.DefaultWeatherCondition,
                    RoadClass: leg.RoadClass);
                var estimate = await _travelTime.EstimateAsync(
                    leg.Mode,
                    leg.DistanceMeters,
                    predictionRequest,
                    leg.BusRouteId,
                    false,
                    cancellationToken);
                total += estimate.TravelMinutes;
                clock = clock.AddMinutes(estimate.TravelMinutes);
                congestionWeighted += estimate.Traffic.PredictedCongestionIndex * Math.Max(0.1, estimate.TravelMinutes);
                trafficMinutes += Math.Max(0.1, estimate.TravelMinutes);
            }

            results.Add(new DepartureWindowOptionDto(
                departure,
                Math.Round(total, 1),
                Math.Round(trafficMinutes > 0 ? congestionWeighted / trafficMinutes : journey.PredictedCongestionIndex, 1),
                0));
        }

        var baseline = results[0].EstimatedDurationMinutes;
        results = results.Select(x => x with { DifferenceMinutes = Math.Round(x.EstimatedDurationMinutes - baseline, 1) }).ToList();
        var bestLater = results.Skip(1).OrderBy(x => x.EstimatedDurationMinutes).FirstOrDefault();
        var benefit = bestLater is null ? 0 : baseline - bestLater.EstimatedDurationMinutes;
        var meaningful = bestLater is not null
            && benefit >= _options.DepartureBenefitMinutes
            && benefit / Math.Max(1d, baseline) >= _options.DepartureBenefitPercent;

        return new DepartureWindowDto
        {
            Options = results,
            HasSuggestion = meaningful,
            SuggestedDepartureTime = meaningful ? bestLater!.DepartureTime : null,
            Suggestion = meaningful
                ? $"Leaving around {bestLater!.DepartureTime:hh:mm tt} is predicted to reduce travel time by approximately {benefit:0.#} minutes."
                : null
        };
    }
}
