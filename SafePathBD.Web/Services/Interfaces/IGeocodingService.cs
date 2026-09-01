using SafePathBD.Web.Models.DTOs.Locations;

namespace SafePathBD.Web.Services.Interfaces;

/// <summary>
/// Application-facing geocoding contract. Controllers and views must not depend on a
/// specific provider's response shape.
/// </summary>
public interface IGeocodingService
{
    Task<IReadOnlyList<PlaceSuggestionDto>> SearchAsync(string query, int limit, CancellationToken cancellationToken = default);

    Task<ResolvedPlaceDto?> ReverseAsync(double latitude, double longitude, CancellationToken cancellationToken = default);
}
