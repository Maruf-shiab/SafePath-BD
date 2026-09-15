using System.Text.Json.Serialization;

namespace SafePathBD.Web.Integrations.Routing;

internal sealed class OsrmRouteResponse
{
    [JsonPropertyName("code")]
    public string? Code { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }

    [JsonPropertyName("routes")]
    public List<OsrmRoute>? Routes { get; set; }
}

internal sealed class OsrmRoute
{
    [JsonPropertyName("distance")]
    public double Distance { get; set; }

    [JsonPropertyName("duration")]
    public double Duration { get; set; }

    [JsonPropertyName("geometry")]
    public OsrmGeoJsonGeometry? Geometry { get; set; }
}

internal sealed class OsrmGeoJsonGeometry
{
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    // GeoJSON is [longitude, latitude]. Conversion happens inside the provider.
    [JsonPropertyName("coordinates")]
    public List<List<double>>? Coordinates { get; set; }
}
