namespace SafePathBD.Web.Integrations.Geocoding;

/// <summary>
/// Configuration for the OpenStreetMap Nominatim geocoder. Contains no secrets;
/// Nominatim requires only a descriptive User-Agent identifying the application.
/// </summary>
public sealed class NominatimOptions
{
    public const string SectionName = "Geocoding:Nominatim";

    public string BaseUrl { get; set; } = "https://nominatim.openstreetmap.org/";

    public string UserAgent { get; set; } = "SafePathBD/1.0 (academic project)";

    /// <summary>ISO country codes used to bias results, comma separated. Empty means worldwide.</summary>
    public string CountryCodes { get; set; } = "bd";

    public string Language { get; set; } = "en";

    public int TimeoutSeconds { get; set; } = 8;

    public int MaxResults { get; set; } = 8;
}
