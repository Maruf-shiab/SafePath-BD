using SafePathBD.Web.Models.DTOs.Emergency;

namespace SafePathBD.Web.Services.Interfaces;

public interface IEmergencyService
{
    Task<IReadOnlyList<EmergencyServiceTypeDto>> GetServiceTypesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Active facilities within <paramref name="radiusKm"/> of the origin, ordered by
    /// straight-line distance.
    /// </summary>
    Task<IReadOnlyList<EmergencyServiceDto>> GetNearbyAsync(
        double latitude,
        double longitude,
        string? serviceTypeName,
        double radiusKm,
        int limit,
        CancellationToken cancellationToken = default);

    Task<EmergencyServiceDto?> GetByIdAsync(ulong emergencyServiceId, CancellationToken cancellationToken = default);
}
