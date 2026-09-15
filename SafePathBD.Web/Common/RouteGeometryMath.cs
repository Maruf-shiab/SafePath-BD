using SafePathBD.Web.Models.DTOs.Routing;

namespace SafePathBD.Web.Common;

/// <summary>
/// Geometry helpers for request-scoped routing. Short point-to-segment distances use a
/// local equirectangular projection, which is appropriate for the small urban distances
/// involved in route/incident proximity checks. Great-circle operations use Haversine.
/// </summary>
public static class RouteGeometryMath
{
    private const double EarthRadiusMeters = 6_371_008.8;

    public readonly record struct RouteBoundingBox(double MinLat, double MinLng, double MaxLat, double MaxLng);

    public sealed record ClosestPointResult(
        RouteCoordinate Point,
        int SegmentIndex,
        double DistanceMeters,
        double SegmentFraction);

    public static double DistancePointToPolylineMeters(
        RouteCoordinate point,
        IReadOnlyList<RouteCoordinate> polyline)
    {
        if (polyline is null || polyline.Count == 0)
        {
            return double.PositiveInfinity;
        }

        if (polyline.Count == 1)
        {
            return GeoMath.HaversineDistanceKm(
                point.Latitude, point.Longitude,
                polyline[0].Latitude, polyline[0].Longitude) * 1000d;
        }

        var minimum = double.PositiveInfinity;
        for (var i = 0; i < polyline.Count - 1; i++)
        {
            minimum = Math.Min(minimum, DistancePointToSegmentMeters(point, polyline[i], polyline[i + 1]));
        }

        return minimum;
    }

    public static double DistancePointToSegmentMeters(
        RouteCoordinate point,
        RouteCoordinate segmentStart,
        RouteCoordinate segmentEnd)
    {
        return ClosestPointOnSegment(point, segmentStart, segmentEnd).DistanceMeters;
    }

    public static ClosestPointResult ClosestPointOnPolyline(
        RouteCoordinate point,
        IReadOnlyList<RouteCoordinate> polyline)
    {
        if (polyline is null || polyline.Count == 0)
        {
            throw new ArgumentException("Route geometry must contain at least one point.", nameof(polyline));
        }

        if (polyline.Count == 1)
        {
            var distance = GeoMath.HaversineDistanceKm(
                point.Latitude, point.Longitude,
                polyline[0].Latitude, polyline[0].Longitude) * 1000d;
            return new ClosestPointResult(polyline[0], 0, distance, 0);
        }

        ClosestPointResult? best = null;
        for (var i = 0; i < polyline.Count - 1; i++)
        {
            var current = ClosestPointOnSegment(point, polyline[i], polyline[i + 1]);
            var withIndex = current with { SegmentIndex = i };
            if (best is null || withIndex.DistanceMeters < best.DistanceMeters)
            {
                best = withIndex;
            }
        }

        return best!;
    }

    public static RouteBoundingBox RouteBoundingBoxFor(IReadOnlyList<RouteCoordinate> polyline)
    {
        if (polyline is null || polyline.Count == 0)
        {
            throw new ArgumentException("Route geometry must contain at least one point.", nameof(polyline));
        }

        return new RouteBoundingBox(
            polyline.Min(p => p.Latitude),
            polyline.Min(p => p.Longitude),
            polyline.Max(p => p.Latitude),
            polyline.Max(p => p.Longitude));
    }

    public static RouteBoundingBox ExpandBoundingBox(RouteBoundingBox box, double meters)
    {
        if (meters <= 0)
        {
            return box;
        }

        var centerLat = (box.MinLat + box.MaxLat) / 2d;
        var latDelta = meters / 111_320d;
        var longitudeMetersPerDegree = Math.Max(111_320d * Math.Cos(ToRadians(centerLat)), 1d);
        var lngDelta = meters / longitudeMetersPerDegree;

        return new RouteBoundingBox(
            Math.Max(-90, box.MinLat - latDelta),
            Math.Max(-180, box.MinLng - lngDelta),
            Math.Min(90, box.MaxLat + latDelta),
            Math.Min(180, box.MaxLng + lngDelta));
    }

    public static double BearingDegrees(RouteCoordinate from, RouteCoordinate to)
    {
        var lat1 = ToRadians(from.Latitude);
        var lat2 = ToRadians(to.Latitude);
        var deltaLng = ToRadians(to.Longitude - from.Longitude);

        var y = Math.Sin(deltaLng) * Math.Cos(lat2);
        var x = Math.Cos(lat1) * Math.Sin(lat2)
                - Math.Sin(lat1) * Math.Cos(lat2) * Math.Cos(deltaLng);

        return NormalizeBearing(ToDegrees(Math.Atan2(y, x)));
    }

    public static RouteCoordinate DestinationPoint(RouteCoordinate origin, double distanceMeters, double bearingDegrees)
    {
        var angularDistance = distanceMeters / EarthRadiusMeters;
        var bearing = ToRadians(NormalizeBearing(bearingDegrees));
        var lat1 = ToRadians(origin.Latitude);
        var lng1 = ToRadians(origin.Longitude);

        var lat2 = Math.Asin(
            Math.Sin(lat1) * Math.Cos(angularDistance)
            + Math.Cos(lat1) * Math.Sin(angularDistance) * Math.Cos(bearing));

        var lng2 = lng1 + Math.Atan2(
            Math.Sin(bearing) * Math.Sin(angularDistance) * Math.Cos(lat1),
            Math.Cos(angularDistance) - Math.Sin(lat1) * Math.Sin(lat2));

        var longitude = ((ToDegrees(lng2) + 540d) % 360d) - 180d;
        return new RouteCoordinate(ToDegrees(lat2), longitude);
    }

    private static ClosestPointResult ClosestPointOnSegment(
        RouteCoordinate point,
        RouteCoordinate start,
        RouteCoordinate end)
    {
        var referenceLat = ToRadians((point.Latitude + start.Latitude + end.Latitude) / 3d);
        var cos = Math.Max(Math.Cos(referenceLat), 0.000001d);

        static double ToY(double latitude) => ToRadians(latitude) * EarthRadiusMeters;
        double ToX(double longitude) => ToRadians(longitude) * EarthRadiusMeters * cos;

        var px = ToX(point.Longitude);
        var py = ToY(point.Latitude);
        var ax = ToX(start.Longitude);
        var ay = ToY(start.Latitude);
        var bx = ToX(end.Longitude);
        var by = ToY(end.Latitude);

        var dx = bx - ax;
        var dy = by - ay;
        var lengthSquared = dx * dx + dy * dy;

        double t;
        if (lengthSquared <= 0.000001d)
        {
            t = 0;
        }
        else
        {
            t = ((px - ax) * dx + (py - ay) * dy) / lengthSquared;
            t = Math.Clamp(t, 0d, 1d);
        }

        var closestX = ax + t * dx;
        var closestY = ay + t * dy;
        var distance = Math.Sqrt(Math.Pow(px - closestX, 2) + Math.Pow(py - closestY, 2));

        var latitude = ToDegrees(closestY / EarthRadiusMeters);
        var longitude = ToDegrees(closestX / (EarthRadiusMeters * cos));

        return new ClosestPointResult(new RouteCoordinate(latitude, longitude), 0, distance, t);
    }

    private static double NormalizeBearing(double bearing) => (bearing % 360d + 360d) % 360d;
    private static double ToRadians(double degrees) => degrees * Math.PI / 180d;
    private static double ToDegrees(double radians) => radians * 180d / Math.PI;
}
