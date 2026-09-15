using System.Globalization;
using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Options;
using SafePathBD.Web.Common;
using SafePathBD.Web.Models.DTOs.Routing;

namespace SafePathBD.Web.Integrations.Routing;

public sealed class OsrmRoutingProvider : IRoutingProvider
{
    public const string HttpClientName = "SafePathBD.OSRM";

    private readonly HttpClient _httpClient;
    private readonly OsrmRoutingOptions _options;
    private readonly ILogger<OsrmRoutingProvider> _logger;

    public OsrmRoutingProvider(
        HttpClient httpClient,
        IOptions<OsrmRoutingOptions> options,
        ILogger<OsrmRoutingProvider> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<IReadOnlyList<ProviderRouteCandidate>> GetRoutesAsync(
        RoutingProviderRequest request,
        CancellationToken cancellationToken = default)
    {
        var points = new List<RouteCoordinate> { request.Start };
        if (request.ViaPoints is { Count: > 0 })
        {
            points.AddRange(request.ViaPoints);
        }
        points.Add(request.Destination);

        if (points.Any(p => !GeoMath.IsValidCoordinate(p.Latitude, p.Longitude)))
        {
            throw new ArgumentException("Routing coordinates are invalid.", nameof(request));
        }

        var coordinatePath = string.Join(';', points.Select(ToOsrmCoordinate));
        var alternatives = request.RequestAlternatives ? "true" : "false";
        var relativeUrl = $"route/v1/{Uri.EscapeDataString(_options.Profile)}/{coordinatePath}" +
                          $"?alternatives={alternatives}&overview=full&geometries=geojson&steps=false";

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.GetAsync(relativeUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new RoutingProviderUnavailableException("The routing provider timed out.", ex);
        }
        catch (HttpRequestException ex)
        {
            throw new RoutingProviderUnavailableException("The routing provider could not be reached.", ex);
        }

        using (response)
        {
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                throw new RoutingProviderNoRouteException("No drivable route was found between these locations.");
            }

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("OSRM returned HTTP {StatusCode}.", (int)response.StatusCode);
                throw new RoutingProviderUnavailableException("The routing provider is temporarily unavailable.");
            }

            OsrmRouteResponse? payload;
            try
            {
                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                payload = await JsonSerializer.DeserializeAsync<OsrmRouteResponse>(stream, cancellationToken: cancellationToken);
            }
            catch (JsonException ex)
            {
                throw new RoutingProviderUnavailableException("The routing provider returned an invalid response.", ex);
            }

            if (payload is null)
            {
                throw new RoutingProviderUnavailableException("The routing provider returned an empty response.");
            }

            if (string.Equals(payload.Code, "NoRoute", StringComparison.OrdinalIgnoreCase)
                || payload.Routes is null
                || payload.Routes.Count == 0)
            {
                throw new RoutingProviderNoRouteException("No drivable route was found between these locations.");
            }

            if (!string.Equals(payload.Code, "Ok", StringComparison.OrdinalIgnoreCase))
            {
                throw new RoutingProviderUnavailableException(payload.Message ?? "The routing provider could not complete the request.");
            }

            var candidates = new List<ProviderRouteCandidate>(payload.Routes.Count);
            for (var index = 0; index < payload.Routes.Count; index++)
            {
                var route = payload.Routes[index];
                var geometry = ParseGeometry(route.Geometry);
                if (geometry.Count < 2 || route.Distance <= 0 || route.Duration < 0)
                {
                    continue;
                }

                candidates.Add(new ProviderRouteCandidate(index, route.Distance, route.Duration, geometry));
            }

            if (candidates.Count == 0)
            {
                throw new RoutingProviderUnavailableException("The routing provider returned routes without usable geometry.");
            }

            return candidates;
        }
    }

    private static string ToOsrmCoordinate(RouteCoordinate point) =>
        string.Format(CultureInfo.InvariantCulture, "{0:0.######},{1:0.######}", point.Longitude, point.Latitude);

    private static IReadOnlyList<RouteCoordinate> ParseGeometry(OsrmGeoJsonGeometry? geometry)
    {
        if (geometry?.Coordinates is null
            || !string.Equals(geometry.Type, "LineString", StringComparison.OrdinalIgnoreCase))
        {
            return Array.Empty<RouteCoordinate>();
        }

        var points = new List<RouteCoordinate>(geometry.Coordinates.Count);
        foreach (var coordinate in geometry.Coordinates)
        {
            if (coordinate.Count < 2)
            {
                continue;
            }

            var longitude = coordinate[0];
            var latitude = coordinate[1];
            if (GeoMath.IsValidCoordinate(latitude, longitude))
            {
                points.Add(new RouteCoordinate(latitude, longitude));
            }
        }

        return points;
    }
}
