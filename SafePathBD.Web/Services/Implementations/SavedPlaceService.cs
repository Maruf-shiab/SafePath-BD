using Microsoft.EntityFrameworkCore;
using SafePathBD.Web.Common;
using SafePathBD.Web.Data;
using SafePathBD.Web.Models.DTOs.Reports;
using SafePathBD.Web.Models.DTOs.SavedPlaces;
using SafePathBD.Web.Models.Entities;
using SafePathBD.Web.Services.Interfaces;

namespace SafePathBD.Web.Services.Implementations;

public sealed class SavedPlaceService : ISavedPlaceService
{
    private readonly SafePathDbContext _db;
    private readonly ILocationService _locations;

    public SavedPlaceService(SafePathDbContext db, ILocationService locations)
    {
        _db = db;
        _locations = locations;
    }

    public async Task<IReadOnlyList<SavedPlaceDto>> GetForUserAsync(
        ulong userId,
        CancellationToken cancellationToken = default)
    {
        if (userId == 0)
        {
            return Array.Empty<SavedPlaceDto>();
        }

        var rows = await _db.SavedPlaces
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderBy(x => x.PlaceType == SavedPlaceTypes.Home ? 0
                : x.PlaceType == SavedPlaceTypes.Office ? 1
                : x.PlaceType == SavedPlaceTypes.University ? 2
                : 3)
            .ThenBy(x => x.PlaceName)
            .Select(x => new
            {
                x.SavedPlaceId,
                x.PlaceName,
                x.PlaceType,
                Latitude = (double)x.Location.Latitude,
                Longitude = (double)x.Location.Longitude,
                x.Location.AddressLine,
                x.Location.AreaName,
                x.Location.City,
                x.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return rows.Select(x => new SavedPlaceDto(
            x.SavedPlaceId,
            x.PlaceName,
            x.PlaceType,
            TypeLabel(x.PlaceType),
            x.Latitude,
            x.Longitude,
            x.AddressLine,
            x.AreaName,
            x.City,
            x.CreatedAt)).ToList();
    }

    public async Task<SavePlaceResult> SaveAsync(
        ulong userId,
        SavePlaceRequest request,
        CancellationToken cancellationToken = default)
    {
        if (userId == 0)
        {
            return new SavePlaceResult(SavePlaceStatus.Invalid, Message: "A signed-in user is required.");
        }

        if (!GeoMath.IsValidCoordinate(request.Latitude, request.Longitude))
        {
            return new SavePlaceResult(SavePlaceStatus.Invalid, Message: "Choose a valid map location first.");
        }

        var placeType = NormalizeType(request.PlaceType);
        if (placeType is null)
        {
            return new SavePlaceResult(SavePlaceStatus.Invalid, Message: "Choose a valid saved-place type.");
        }

        var placeName = NormalizeName(placeType, request.PlaceName);
        if (placeName is null)
        {
            return new SavePlaceResult(SavePlaceStatus.Invalid, Message: "Enter a name between 2 and 120 characters.");
        }

        // HOME and OFFICE are intentionally singletons. Saving them again updates the existing
        // shortcut rather than creating Home 2 / Work 2. Other bookmark types may be multiple.
        SavedPlaces? existing = null;
        if (placeType is SavedPlaceTypes.Home or SavedPlaceTypes.Office)
        {
            existing = await _db.SavedPlaces
                .FirstOrDefaultAsync(x => x.UserId == userId && x.PlaceType == placeType, cancellationToken);
        }

        var nameConflict = await _db.SavedPlaces
            .AsNoTracking()
            .AnyAsync(x => x.UserId == userId
                && x.PlaceName == placeName
                && (existing == null || x.SavedPlaceId != existing.SavedPlaceId), cancellationToken);

        if (nameConflict)
        {
            return new SavePlaceResult(
                SavePlaceStatus.DuplicateName,
                Message: "You already have a saved place with this name. Choose another name.");
        }

        var location = await _locations.ResolveOrCreateAsync(new ReportLocationInput(
            request.Latitude,
            request.Longitude,
            Trim(request.AddressLine, 500),
            null,
            Trim(request.AreaName, 150),
            Trim(request.City, 100),
            Trim(request.District, 100),
            Trim(request.Provider, 20),
            Trim(request.ExternalPlaceId, 255)), cancellationToken);

        // ResolveOrCreate may return a new tracked location. Save it first so LocationId is
        // available for the saved_places foreign key. No schema changes are involved.
        if (location.LocationId == 0)
        {
            await _db.SaveChangesAsync(cancellationToken);
        }

        SavedPlaces entity;
        if (existing is not null)
        {
            existing.LocationId = location.LocationId;
            existing.PlaceName = placeName;
            entity = existing;
        }
        else
        {
            entity = new SavedPlaces
            {
                UserId = userId,
                LocationId = location.LocationId,
                PlaceName = placeName,
                PlaceType = placeType
            };
            _db.SavedPlaces.Add(entity);
        }

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return new SavePlaceResult(
                SavePlaceStatus.DuplicateName,
                Message: "That saved-place name is already in use.");
        }

        var dto = new SavedPlaceDto(
            entity.SavedPlaceId,
            entity.PlaceName,
            entity.PlaceType,
            TypeLabel(entity.PlaceType),
            (double)location.Latitude,
            (double)location.Longitude,
            location.AddressLine,
            location.AreaName,
            location.City,
            entity.CreatedAt);

        return new SavePlaceResult(SavePlaceStatus.Success, dto,
            existing is null ? "Location saved." : $"{TypeLabel(placeType)} location updated.");
    }

    public async Task<bool> DeleteAsync(
        ulong userId,
        ulong savedPlaceId,
        CancellationToken cancellationToken = default)
    {
        if (userId == 0 || savedPlaceId == 0)
        {
            return false;
        }

        var entity = await _db.SavedPlaces
            .FirstOrDefaultAsync(x => x.SavedPlaceId == savedPlaceId && x.UserId == userId, cancellationToken);

        if (entity is null)
        {
            return false;
        }

        _db.SavedPlaces.Remove(entity);
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static string? NormalizeType(string? value)
    {
        var normalized = value?.Trim().ToUpperInvariant();
        if (normalized == "WORK") normalized = SavedPlaceTypes.Office;
        if (normalized == "BOOKMARK") normalized = SavedPlaceTypes.Favorite;
        return normalized is not null && SavedPlaceTypes.All.Contains(normalized) ? normalized : null;
    }

    private static string? NormalizeName(string placeType, string? value)
    {
        var automatic = placeType switch
        {
            SavedPlaceTypes.Home => "Home",
            SavedPlaceTypes.Office => "Work",
            _ => null
        };

        var candidate = automatic ?? value?.Trim();
        if (string.IsNullOrWhiteSpace(candidate) || candidate.Length < 2)
        {
            return null;
        }

        return candidate.Length <= 120 ? candidate : candidate[..120];
    }

    private static string TypeLabel(string placeType) => placeType switch
    {
        SavedPlaceTypes.Home => "Home",
        SavedPlaceTypes.Office => "Work",
        SavedPlaceTypes.University => "University",
        SavedPlaceTypes.Favorite => "Bookmark",
        _ => "Saved place"
    };

    private static string? Trim(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }
}
