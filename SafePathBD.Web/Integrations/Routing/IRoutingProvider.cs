using SafePathBD.Web.Models.DTOs.Routing;

namespace SafePathBD.Web.Integrations.Routing;

public sealed record RoutingProviderRequest(
    RouteCoordinate Start,
    RouteCoordinate Destination,
    IReadOnlyList<RouteCoordinate>? ViaPoints = null,
    bool RequestAlternatives = true);

public sealed record ProviderRouteCandidate(
    int ProviderIndex,
    double DistanceMeters,
    double DurationSeconds,
    IReadOnlyList<RouteCoordinate> Geometry);

public interface IRoutingProvider
{
    Task<IReadOnlyList<ProviderRouteCandidate>> GetRoutesAsync(
        RoutingProviderRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class RoutingProviderUnavailableException : Exception
{
    public RoutingProviderUnavailableException(string message, Exception? innerException = null)
        : base(message, innerException) { }
}

public sealed class RoutingProviderNoRouteException : Exception
{
    public RoutingProviderNoRouteException(string message) : base(message) { }
}
