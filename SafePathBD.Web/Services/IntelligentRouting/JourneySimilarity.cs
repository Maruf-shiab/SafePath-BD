using SafePathBD.Web.Common;
using SafePathBD.Web.Models.DTOs.Routing;

namespace SafePathBD.Web.Services.IntelligentRouting;

/// <summary>
/// Spatial diversity calculation for intelligent journey alternatives. Internal edge ids are
/// request-local and therefore cannot measure overlap between two provider alternatives; this
/// helper compares the actual geometries instead.
/// </summary>
public static class JourneySimilarity
{
    public static double Calculate(JourneyPath first, JourneyPath second, double proximityMeters = 60d)
    {
        var a = Flatten(first);
        var b = Flatten(second);
        if (a.Count < 2 || b.Count < 2)
        {
            return 0d;
        }

        var aSample = Sample(a, 80);
        var bSample = Sample(b, 80);
        var aOnB = aSample.Count == 0 ? 0d : aSample.Count(p => RouteGeometryMath.DistancePointToPolylineMeters(p, b) <= proximityMeters) / (double)aSample.Count;
        var bOnA = bSample.Count == 0 ? 0d : bSample.Count(p => RouteGeometryMath.DistancePointToPolylineMeters(p, a) <= proximityMeters) / (double)bSample.Count;
        var spatial = (aOnB + bOnA) / 2d;

        var firstModes = first.Steps.Where(s => s.Edge is not null).Select(s => s.Mode).DistinctAdjacent().ToArray();
        var secondModes = second.Steps.Where(s => s.Edge is not null).Select(s => s.Mode).DistinctAdjacent().ToArray();
        var modeSimilarity = firstModes.SequenceEqual(secondModes, StringComparer.OrdinalIgnoreCase) ? 1d : 0d;
        return Math.Clamp(spatial * 0.90d + modeSimilarity * 0.10d, 0d, 1d);
    }

    private static IReadOnlyList<RouteCoordinate> Flatten(JourneyPath path)
    {
        var result = new List<RouteCoordinate>();
        foreach (var geometry in path.Steps.Where(s => s.Edge is not null).Select(s => s.Edge!.Geometry))
        {
            foreach (var point in geometry)
            {
                if (result.Count == 0 || !Same(result[^1], point))
                {
                    result.Add(point);
                }
            }
        }
        return result;
    }

    private static IReadOnlyList<RouteCoordinate> Sample(IReadOnlyList<RouteCoordinate> points, int maxPoints)
    {
        if (points.Count <= maxPoints)
        {
            return points;
        }
        var stride = Math.Max(1, (int)Math.Ceiling(points.Count / (double)maxPoints));
        var sampled = new List<RouteCoordinate>();
        for (var i = 0; i < points.Count; i += stride)
        {
            sampled.Add(points[i]);
        }
        if (!Same(sampled[^1], points[^1]))
        {
            sampled.Add(points[^1]);
        }
        return sampled;
    }

    private static bool Same(RouteCoordinate a, RouteCoordinate b) =>
        Math.Abs(a.Latitude - b.Latitude) < 0.0000001d && Math.Abs(a.Longitude - b.Longitude) < 0.0000001d;

    private static IEnumerable<T> DistinctAdjacent<T>(this IEnumerable<T> source)
    {
        var comparer = EqualityComparer<T>.Default;
        var first = true;
        T? previous = default;
        foreach (var item in source)
        {
            if (first || !comparer.Equals(previous!, item))
            {
                yield return item;
                previous = item;
                first = false;
            }
        }
    }
}
