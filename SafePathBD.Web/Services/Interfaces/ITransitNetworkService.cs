using SafePathBD.Web.Models.DTOs.Routing;

namespace SafePathBD.Web.Services.Interfaces;

public sealed record TransitStopInfo(string StopId, string StopName, double Latitude, double Longitude, string Area);
public sealed record TransitRouteInfo(string RouteId, string RouteName, string RouteShortName, IReadOnlyList<string> StopIds);

public interface ITransitNetworkService
{
    IReadOnlyList<TransitStopInfo> Stops { get; }
    IReadOnlyList<TransitRouteInfo> Routes { get; }
    IReadOnlyList<TransitStopInfo> FindStopsNear(RouteCoordinate coordinate, double maxMeters);
    IReadOnlyList<TransitRouteInfo> GetRoutesServingStop(string stopId);
    double GetExpectedWaitMinutes(string routeId, DateTimeOffset at);
}
