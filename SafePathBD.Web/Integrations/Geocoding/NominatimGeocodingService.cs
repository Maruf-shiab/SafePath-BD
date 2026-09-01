using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using SafePathBD.Web.Common;
using SafePathBD.Web.Models.DTOs.Locations;
using SafePathBD.Web.Services.Interfaces;

namespace SafePathBD.Web.Integrations.Geocoding;

/// <summary>
/// OpenStreetMap Nominatim implementation of <see cref="IGeocodingService"/>.
/// Provider response shapes stay inside this class.
/// </summary>
public sealed class NominatimGeocodingService : IGeocodingService
{
    public const string ProviderName = "OSM";
    public const string HttpClientName = "nominatim";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;
    private readonly NominatimOptions _options;
    private readonly ILogger<NominatimGeocodingService> _logger;

    public NominatimGeocodingService(
        HttpClient http,
        IOptions<NominatimOptions> options,
        ILogger<NominatimGeocodingService> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<IReadOnlyList<PlaceSuggestionDto>> SearchAsync(
        string query,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var trimmed = query.Trim();
        if (trimmed.Length == 0)
        {
            return Array.Empty<PlaceSuggestionDto>();
        }

        var take = Math.Clamp(limit, 1, _options.MaxResults);
        var url = $"search?format=jsonv2&addressdetails=1&limit={take}&accept-language={Uri.EscapeDataString(_options.Language)}&q={Uri.EscapeDataString(trimmed)}";

        if (!string.IsNullOrWhiteSpace(_options.CountryCodes))
        {
            url += $"&countrycodes={Uri.EscapeDataString(_options.CountryCodes)}";
        }

        var places = await GetAsync<List<NominatimPlace>>(url, cancellationToken);
        if (places is null)
        {
            throw new GeocodingUnavailableException("The location search provider is unavailable.");
        }

        return places
            .Where(p => TryParseCoordinate(p.Lat, p.Lon, out _, out _))
            .Select(ToSuggestion)
            .ToList();
    }

    public async Task<ResolvedPlaceDto?> ReverseAsync(
        double latitude,
        double longitude,
        CancellationToken cancellationToken = default)
    {
        var lat = latitude.ToString("0.#######", CultureInfo.InvariantCulture);
        var lon = longitude.ToString("0.#######", CultureInfo.InvariantCulture);
        var url = $"reverse?format=jsonv2&addressdetails=1&zoom=18&accept-language={Uri.EscapeDataString(_options.Language)}&lat={lat}&lon={lon}";

        var place = await GetAsync<NominatimPlace>(url, cancellationToken);
        if (place is null)
        {
            throw new GeocodingUnavailableException("The address lookup provider is unavailable.");
        }

        if (string.IsNullOrWhiteSpace(place.DisplayName))
        {
            return null;
        }

        var address = place.Address;

        return new ResolvedPlaceDto(
            latitude,
            longitude,
            place.DisplayName!,
            BuildAddressLine(place),
            address?.Suburb ?? address?.Neighbourhood ?? address?.Village,
            address?.City ?? address?.Town ?? address?.Municipality,
            address?.StateDistrict ?? address?.County ?? address?.State,
            ProviderName);
    }

    private async Task<T?> GetAsync<T>(string url, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _http.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Nominatim returned {StatusCode} for a geocoding request.", (int)response.StatusCode);
                return default;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            return await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions, cancellationToken);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Nominatim request timed out.");
            return default;
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException)
        {
            _logger.LogWarning(ex, "Nominatim request failed.");
            return default;
        }
    }

    private static PlaceSuggestionDto ToSuggestion(NominatimPlace place)
    {
        TryParseCoordinate(place.Lat, place.Lon, out var lat, out var lon);

        var display = place.DisplayName ?? place.Name ?? $"{lat}, {lon}";
        var shortName = place.Name;

        if (string.IsNullOrWhiteSpace(shortName))
        {
            shortName = display.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault() ?? display;
        }

        return new PlaceSuggestionDto(display, shortName, lat, lon, ProviderName, place.OsmId?.ToString());
    }

    private static string? BuildAddressLine(NominatimPlace place)
    {
        var address = place.Address;
        if (address is null)
        {
            return place.DisplayName;
        }

        var parts = new[] { address.Road, address.Suburb, address.City ?? address.Town ?? address.Village }
            .Where(part => !string.IsNullOrWhiteSpace(part));

        var line = string.Join(", ", parts);
        return line.Length > 0 ? line : place.DisplayName;
    }

    private static bool TryParseCoordinate(string? lat, string? lon, out double latitude, out double longitude)
    {
        latitude = 0;
        longitude = 0;

        return double.TryParse(lat, NumberStyles.Float, CultureInfo.InvariantCulture, out latitude)
               && double.TryParse(lon, NumberStyles.Float, CultureInfo.InvariantCulture, out longitude)
               && GeoMath.IsValidCoordinate(latitude, longitude);
    }

    private sealed class NominatimPlace
    {
        [JsonPropertyName("osm_id")]
        public long? OsmId { get; set; }

        public string? Lat { get; set; }

        public string? Lon { get; set; }

        public string? Name { get; set; }

        [JsonPropertyName("display_name")]
        public string? DisplayName { get; set; }

        public NominatimAddress? Address { get; set; }
    }

    private sealed class NominatimAddress
    {
        public string? Road { get; set; }
        public string? Neighbourhood { get; set; }
        public string? Suburb { get; set; }
        public string? Village { get; set; }
        public string? Town { get; set; }
        public string? City { get; set; }
        public string? Municipality { get; set; }
        public string? County { get; set; }

        [JsonPropertyName("state_district")]
        public string? StateDistrict { get; set; }

        public string? State { get; set; }
    }
}

public sealed class GeocodingUnavailableException : Exception
{
    public GeocodingUnavailableException(string message) : base(message)
    {
    }
}
