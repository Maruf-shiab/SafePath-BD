using SafePathBD.Web.Common;
using SafePathBD.Web.Models.DTOs.Routing;

namespace SafePathBD.Tests;

public class RouteGeometryMathTests
{
    private static readonly RouteCoordinate A = new(23.7500, 90.3700);
    private static readonly RouteCoordinate B = new(23.7500, 90.3800);

    [Fact]
    public void PointOnRoute_HasNearZeroDistance()
    {
        var distance = RouteGeometryMath.DistancePointToPolylineMeters(
            new RouteCoordinate(23.7500, 90.3750), new[] { A, B });

        Assert.InRange(distance, 0, 0.5);
    }

    [Fact]
    public void PointNearRoute_IsMeasuredPerpendicularly()
    {
        var distance = RouteGeometryMath.DistancePointToSegmentMeters(
            new RouteCoordinate(23.75018, 90.3750), A, B);

        Assert.InRange(distance, 18, 22);
    }

    [Fact]
    public void PointBeyondSegment_UsesNearestEndpoint()
    {
        var point = new RouteCoordinate(23.7500, 90.3820);
        var segmentDistance = RouteGeometryMath.DistancePointToSegmentMeters(point, A, B);
        var endpointDistance = GeoMath.HaversineDistanceKm(point.Latitude, point.Longitude, B.Latitude, B.Longitude) * 1000;

        Assert.InRange(Math.Abs(segmentDistance - endpointDistance), 0, 1.5);
    }

    [Fact]
    public void FarPoint_IsNotMistakenForRouteProximity()
    {
        var distance = RouteGeometryMath.DistancePointToPolylineMeters(
            new RouteCoordinate(23.7600, 90.3750), new[] { A, B });

        Assert.True(distance > 1000);
    }

    [Fact]
    public void ZeroLengthSegment_RemainsStable()
    {
        var distance = RouteGeometryMath.DistancePointToSegmentMeters(
            new RouteCoordinate(23.7501, 90.3700), A, A);

        Assert.InRange(distance, 10, 12.5);
    }

    [Fact]
    public void BoundingBoxExpansion_GrowsInEveryDirection()
    {
        var box = RouteGeometryMath.RouteBoundingBoxFor(new[] { A, B });
        var expanded = RouteGeometryMath.ExpandBoundingBox(box, 50);

        Assert.True(expanded.MinLat < box.MinLat);
        Assert.True(expanded.MinLng < box.MinLng);
        Assert.True(expanded.MaxLat > box.MaxLat);
        Assert.True(expanded.MaxLng > box.MaxLng);
    }

    [Fact]
    public void DestinationPoint_ProducesConfiguredOffset()
    {
        var destination = RouteGeometryMath.DestinationPoint(A, 600, 90);
        var distance = GeoMath.HaversineDistanceKm(A.Latitude, A.Longitude, destination.Latitude, destination.Longitude) * 1000;

        Assert.InRange(distance, 598, 602);
    }
}
