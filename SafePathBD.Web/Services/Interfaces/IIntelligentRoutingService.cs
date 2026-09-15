using SafePathBD.Web.Models.DTOs.IntelligentRouting;

namespace SafePathBD.Web.Services.Interfaces;

public static class IntelligentRouteStatuses
{
    public const string Success = "SUCCESS";
    public const string Invalid = "INVALID";
    public const string NoRoute = "NO_ROUTE";
    public const string ProviderUnavailable = "PROVIDER_UNAVAILABLE";
    public const string SearchExpired = "SEARCH_EXPIRED";
    public const string Forbidden = "FORBIDDEN";
}

public interface IIntelligentRoutingService
{
    Task<IntelligentRouteSearchResult> SearchAsync(
        IntelligentRouteRequest request,
        ulong? userId,
        CancellationToken cancellationToken = default);

    Task<(string Status, string? Message, WhatIfDisruptionResponseDto? Response)> SimulateDisruptionAsync(
        WhatIfDisruptionRequest request,
        ulong? userId,
        CancellationToken cancellationToken = default);
}

public interface IDepartureWindowService
{
    Task<DepartureWindowDto> EvaluateAsync(
        JourneyOptionDto journey,
        IntelligentRouteRequest request,
        CancellationToken cancellationToken = default);
}
