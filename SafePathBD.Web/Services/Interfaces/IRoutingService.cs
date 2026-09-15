using SafePathBD.Web.Models.DTOs.Routing;

namespace SafePathBD.Web.Services.Interfaces;

public enum RouteSearchStatus
{
    Success,
    InvalidRequest,
    NoRoute,
    ProviderUnavailable
}

public sealed record RouteSearchResult(RouteSearchStatus Status, string? Message, RouteSearchResponseDto? Response)
{
    public static RouteSearchResult Success(RouteSearchResponseDto response) => new(RouteSearchStatus.Success, null, response);
    public static RouteSearchResult Invalid(string message) => new(RouteSearchStatus.InvalidRequest, message, null);
    public static RouteSearchResult NoRoute(string message) => new(RouteSearchStatus.NoRoute, message, null);
    public static RouteSearchResult Unavailable(string message) => new(RouteSearchStatus.ProviderUnavailable, message, null);
}

public enum RouteRecheckStatus
{
    Success,
    InvalidRequest,
    SearchExpired,
    Forbidden
}

public sealed record RouteRecheckResult(RouteRecheckStatus Status, string? Message, RouteRecheckResponseDto? Response)
{
    public static RouteRecheckResult Success(RouteRecheckResponseDto response) => new(RouteRecheckStatus.Success, null, response);
    public static RouteRecheckResult Invalid(string message) => new(RouteRecheckStatus.InvalidRequest, message, null);
    public static RouteRecheckResult Expired(string message) => new(RouteRecheckStatus.SearchExpired, message, null);
    public static RouteRecheckResult Forbidden(string message) => new(RouteRecheckStatus.Forbidden, message, null);
}

public interface IRoutingService
{
    Task<RouteSearchResult> SearchAsync(
        RouteSearchRequest request,
        ulong? userId,
        CancellationToken cancellationToken = default);

    Task<RouteRecheckResult> RecheckAsync(
        RouteRecheckRequest request,
        ulong? userId,
        CancellationToken cancellationToken = default);
}
