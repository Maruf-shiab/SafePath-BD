using SafePathBD.Web.Models.DTOs.SavedPlaces;

namespace SafePathBD.Web.Services.Interfaces;

public interface ISavedPlaceService
{
    Task<IReadOnlyList<SavedPlaceDto>> GetForUserAsync(ulong userId, CancellationToken cancellationToken = default);
    Task<SavePlaceResult> SaveAsync(ulong userId, SavePlaceRequest request, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(ulong userId, ulong savedPlaceId, CancellationToken cancellationToken = default);
}
