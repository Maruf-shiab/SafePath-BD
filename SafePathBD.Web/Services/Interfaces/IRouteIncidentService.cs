using SafePathBD.Web.Models.DTOs.Routing;

namespace SafePathBD.Web.Services.Interfaces;

public sealed record RouteIncidentAssessment(
    string IncidentState,
    IReadOnlyList<RouteIncidentDto> Incidents,
    DateTime CheckedAt);

public interface IRouteIncidentService
{
    Task<RouteIncidentAssessment> AssessRouteAsync(
        IReadOnlyList<RouteCoordinate> geometry,
        CancellationToken cancellationToken = default);
}
