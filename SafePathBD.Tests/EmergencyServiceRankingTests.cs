using SafePathBD.Web.Models.Entities;
using SafePathBD.Web.Services.Implementations;

namespace SafePathBD.Tests;

public class EmergencyServiceRankingTests
{
    private const double OriginLat = 23.7386;
    private const double OriginLng = 90.3956;

    private static VwEmergencyServicesWithLocation Facility(
        ulong id,
        string name,
        string type,
        double lat,
        double lng,
        bool is24Hours = false,
        bool verified = true) =>
        new()
        {
            EmergencyServiceId = id,
            ServiceName = name,
            ServiceTypeName = type,
            Latitude = (decimal)lat,
            Longitude = (decimal)lng,
            Is24Hours = is24Hours,
            IsVerified = verified,
            IsActive = true,
            City = "Dhaka"
        };

    private static List<VwEmergencyServicesWithLocation> Sample() =>
    [
        // Roughly 15 km north.
        Facility(1, "Far Hospital", "Hospital", 23.8759, 90.3795),
        // Roughly 1.1 km north.
        Facility(2, "Near Police Post", "Police Station", 23.7486, 90.3956),
        // Roughly 5.5 km north.
        Facility(3, "Mid Fire Station", "Fire Service", 23.7886, 90.3956, is24Hours: true)
    ];

    [Fact]
    public void RankByDistance_OrdersNearestFirst()
    {
        var results = EmergencyService.RankByDistance(Sample(), OriginLat, OriginLng, 50, 10);

        Assert.Equal(new ulong[] { 2, 3, 1 }, results.Select(r => r.EmergencyServiceId));
    }

    [Fact]
    public void RankByDistance_ExcludesFacilitiesBeyondTheRadius()
    {
        var results = EmergencyService.RankByDistance(Sample(), OriginLat, OriginLng, 6, 10);

        Assert.Equal(2, results.Count);
        Assert.DoesNotContain(results, r => r.EmergencyServiceId == 1);
    }

    [Fact]
    public void RankByDistance_RespectsTheLimit()
    {
        var results = EmergencyService.RankByDistance(Sample(), OriginLat, OriginLng, 50, 2);

        Assert.Equal(2, results.Count);
        Assert.Equal(2ul, results[0].EmergencyServiceId);
    }

    [Fact]
    public void RankByDistance_ReportsDistanceRoundedToTwoDecimals()
    {
        var results = EmergencyService.RankByDistance(Sample(), OriginLat, OriginLng, 50, 10);
        var nearest = results[0];

        Assert.NotNull(nearest.StraightLineDistanceKm);
        Assert.InRange(nearest.StraightLineDistanceKm!.Value, 1.0, 1.3);
        Assert.Equal(Math.Round(nearest.StraightLineDistanceKm.Value, 2), nearest.StraightLineDistanceKm.Value);
    }

    [Fact]
    public void RankByDistance_MapsTheFieldsTheUiNeeds()
    {
        var results = EmergencyService.RankByDistance(Sample(), OriginLat, OriginLng, 50, 10);
        var fireStation = results.Single(r => r.EmergencyServiceId == 3);

        Assert.Equal("Mid Fire Station", fireStation.ServiceName);
        Assert.Equal("Fire Service", fireStation.ServiceTypeName);
        Assert.Equal("Dhaka", fireStation.City);
        Assert.True(fireStation.Is24Hours);
        Assert.True(fireStation.IsVerified);
    }

    [Fact]
    public void RankByDistance_ReturnsEmptyWhenNothingIsInRange()
    {
        var results = EmergencyService.RankByDistance(Sample(), OriginLat, OriginLng, 0.2, 10);

        Assert.Empty(results);
    }
}
