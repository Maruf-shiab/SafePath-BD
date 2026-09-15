using System.Globalization;
using Microsoft.Extensions.Options;
using SafePathBD.Web.Common;
using SafePathBD.Web.Models.DTOs.Routing;
using SafePathBD.Web.Services.Interfaces;

namespace SafePathBD.Web.Services.Implementations;

public sealed class TransitNetworkService : ITransitNetworkService
{
    private readonly IReadOnlyDictionary<string, TransitStopInfo> _stops;
    private readonly IReadOnlyDictionary<string, TransitRouteInfo> _routes;
    private readonly IReadOnlyDictionary<string, List<TransitRouteInfo>> _routesByStop;
    private readonly IReadOnlyDictionary<string, List<FrequencyWindow>> _frequencies;

    public IReadOnlyList<TransitStopInfo> Stops => _stops.Values.OrderBy(s => s.StopId).ToArray();
    public IReadOnlyList<TransitRouteInfo> Routes => _routes.Values.OrderBy(r => r.RouteId).ToArray();

    public TransitNetworkService(
        IWebHostEnvironment environment,
        IOptions<IntelligentMobilityOptions> options,
        ILogger<TransitNetworkService> logger)
    {
        var root = Resolve(environment.ContentRootPath, options.Value.TransitDataPath);
        _stops = LoadStops(Path.Combine(root, "bus_stops.csv"));
        var routeNames = LoadRouteNames(Path.Combine(root, "bus_routes.csv"));
        var routeStops = LoadRouteStops(Path.Combine(root, "bus_route_stops.csv"));
        _routes = routeNames.ToDictionary(
            pair => pair.Key,
            pair => new TransitRouteInfo(
                pair.Key,
                pair.Value.Name,
                pair.Value.ShortName,
                routeStops.TryGetValue(pair.Key, out var ids) ? ids : Array.Empty<string>()),
            StringComparer.OrdinalIgnoreCase);

        var byStop = new Dictionary<string, List<TransitRouteInfo>>(StringComparer.OrdinalIgnoreCase);
        foreach (var route in _routes.Values)
        {
            foreach (var stopId in route.StopIds)
            {
                if (!byStop.TryGetValue(stopId, out var list))
                {
                    list = new List<TransitRouteInfo>();
                    byStop[stopId] = list;
                }
                list.Add(route);
            }
        }
        _routesByStop = byStop;
        _frequencies = LoadFrequencies(Path.Combine(root, "bus_frequencies.csv"));

        logger.LogInformation(
            "Loaded SafePath synthetic transit development data: stops={Stops}, routes={Routes}.",
            _stops.Count,
            _routes.Count);
    }

    public IReadOnlyList<TransitStopInfo> FindStopsNear(RouteCoordinate coordinate, double maxMeters) =>
        _stops.Values
            .Select(stop => new
            {
                Stop = stop,
                Distance = GeoMath.HaversineDistanceKm(
                    coordinate.Latitude,
                    coordinate.Longitude,
                    stop.Latitude,
                    stop.Longitude) * 1000d
            })
            .Where(x => x.Distance <= maxMeters)
            .OrderBy(x => x.Distance)
            .Select(x => x.Stop)
            .ToArray();

    public IReadOnlyList<TransitRouteInfo> GetRoutesServingStop(string stopId) =>
        _routesByStop.TryGetValue(stopId, out var routes) ? routes : Array.Empty<TransitRouteInfo>();

    public double GetExpectedWaitMinutes(string routeId, DateTimeOffset at)
    {
        if (!_frequencies.TryGetValue(routeId, out var windows) || windows.Count == 0)
        {
            return 8;
        }

        var hour = at.Hour;
        var window = windows.FirstOrDefault(w => hour >= w.StartHour && hour < w.EndHour)
            ?? windows.OrderBy(w => Math.Abs(w.StartHour - hour)).First();
        return Math.Round(window.HeadwayMinutes / 2d, 1);
    }

    private static IReadOnlyDictionary<string, TransitStopInfo> LoadStops(string path)
    {
        var rows = RequireRows(path);
        var header = Header(rows[0]);
        var result = new Dictionary<string, TransitStopInfo>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows.Skip(1))
        {
            var id = Get(row, header, "stop_id");
            if (string.IsNullOrWhiteSpace(id)) continue;
            result[id] = new TransitStopInfo(
                id,
                Get(row, header, "stop_name"),
                ParseDouble(Get(row, header, "latitude")),
                ParseDouble(Get(row, header, "longitude")),
                Get(row, header, "area"));
        }
        return result;
    }

    private static IReadOnlyDictionary<string, (string Name, string ShortName)> LoadRouteNames(string path)
    {
        var rows = RequireRows(path);
        var header = Header(rows[0]);
        var result = new Dictionary<string, (string, string)>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows.Skip(1))
        {
            var id = Get(row, header, "route_id");
            if (string.IsNullOrWhiteSpace(id)) continue;
            result[id] = (Get(row, header, "route_name"), Get(row, header, "route_short_name"));
        }
        return result;
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<string>> LoadRouteStops(string path)
    {
        var rows = RequireRows(path);
        var header = Header(rows[0]);
        return rows.Skip(1)
            .Select(row => new
            {
                RouteId = Get(row, header, "route_id"),
                StopId = Get(row, header, "stop_id"),
                Sequence = ParseInt(Get(row, header, "sequence"))
            })
            .Where(x => !string.IsNullOrWhiteSpace(x.RouteId) && !string.IsNullOrWhiteSpace(x.StopId))
            .GroupBy(x => x.RouteId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<string>)g.OrderBy(x => x.Sequence).Select(x => x.StopId).ToArray(),
                StringComparer.OrdinalIgnoreCase);
    }

    private static IReadOnlyDictionary<string, List<FrequencyWindow>> LoadFrequencies(string path)
    {
        var rows = RequireRows(path);
        var header = Header(rows[0]);
        return rows.Skip(1)
            .Select(row => new FrequencyWindow(
                Get(row, header, "route_id"),
                ParseInt(Get(row, header, "start_hour")),
                ParseInt(Get(row, header, "end_hour")),
                ParseDouble(Get(row, header, "headway_minutes"))))
            .Where(x => !string.IsNullOrWhiteSpace(x.RouteId))
            .GroupBy(x => x.RouteId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.OrderBy(x => x.StartHour).ToList(), StringComparer.OrdinalIgnoreCase);
    }

    private static List<string[]> RequireRows(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("SafePath synthetic transit development data is missing.", path);
        }
        return SimpleCsv.ReadRows(path).ToList();
    }

    private static Dictionary<string, int> Header(string[] row) =>
        row.Select((name, index) => (name: name.Trim(), index))
            .ToDictionary(x => x.name, x => x.index, StringComparer.OrdinalIgnoreCase);

    private static string Get(string[] row, IReadOnlyDictionary<string, int> header, string name) =>
        header.TryGetValue(name, out var index) && index < row.Length ? row[index].Trim() : string.Empty;

    private static double ParseDouble(string value) =>
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0;

    private static int ParseInt(string value) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0;

    private static string Resolve(string root, string relativeOrAbsolute) =>
        Path.IsPathRooted(relativeOrAbsolute) ? relativeOrAbsolute : Path.Combine(root, relativeOrAbsolute.Replace('/', Path.DirectorySeparatorChar));

    private sealed record FrequencyWindow(string RouteId, int StartHour, int EndHour, double HeadwayMinutes);
}
