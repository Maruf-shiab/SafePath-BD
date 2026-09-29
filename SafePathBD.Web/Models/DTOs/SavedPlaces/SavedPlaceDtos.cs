namespace SafePathBD.Web.Models.DTOs.SavedPlaces;

public static class SavedPlaceTypes
{
    public const string Home = "HOME";
    public const string Office = "OFFICE";
    public const string University = "UNIVERSITY";
    public const string Favorite = "FAVORITE";
    public const string Other = "OTHER";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        Home, Office, University, Favorite, Other
    };
}

public sealed record SavedPlaceDto(
    ulong SavedPlaceId,
    string PlaceName,
    string PlaceType,
    string TypeLabel,
    double Latitude,
    double Longitude,
    string? AddressLine,
    string? AreaName,
    string? City,
    DateTime CreatedAt);

public sealed record SavePlaceRequest(
    string? PlaceName,
    string PlaceType,
    double Latitude,
    double Longitude,
    string? AddressLine = null,
    string? AreaName = null,
    string? City = null,
    string? District = null,
    string? Provider = null,
    string? ExternalPlaceId = null);

public enum SavePlaceStatus
{
    Success,
    Invalid,
    DuplicateName,
    Failed
}

public sealed record SavePlaceResult(SavePlaceStatus Status, SavedPlaceDto? Place = null, string? Message = null)
{
    public bool Succeeded => Status == SavePlaceStatus.Success;
}
