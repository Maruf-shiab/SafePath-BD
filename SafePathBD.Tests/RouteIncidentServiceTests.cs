using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SafePathBD.Web.Common;
using SafePathBD.Web.Integrations.Routing;
using SafePathBD.Web.Models.DTOs.Routing;
using SafePathBD.Web.Models.Entities;
using SafePathBD.Web.Services.Implementations;

namespace SafePathBD.Tests;

public class RouteIncidentServiceTests
{
    private static readonly IReadOnlyList<RouteCoordinate> Route = new[]
    {
        new RouteCoordinate(23.7500, 90.3700),
        new RouteCoordinate(23.7500, 90.3800)
    };

    [Fact]
    public async Task VerifiedAccidentTwentyMetresFromRoute_IsDetected()
    {
        using var ctx = new ReportTestContext();
        AddAccident(ctx, 100, ReportStatusCodes.Verified, isPublic: true, severityId: 3, latitude: 23.75018);
        var service = CreateService(ctx);

        var result = await service.AssessRouteAsync(Route);

        var incident = Assert.Single(result.Incidents);
        Assert.Equal(RouteIncidentStates.Affected, result.IncidentState);
        Assert.Equal("Vehicle Collision", incident.AccidentType);
        Assert.InRange(incident.DistanceFromRouteMeters, 18, 22);
    }

    [Fact]
    public async Task SameAccidentOutsideThreshold_IsNotDetected()
    {
        using var ctx = new ReportTestContext();
        AddAccident(ctx, 101, ReportStatusCodes.Verified, isPublic: true, severityId: 3, latitude: 23.7520);
        var service = CreateService(ctx);

        var result = await service.AssessRouteAsync(Route);

        Assert.Empty(result.Incidents);
        Assert.Equal(RouteIncidentStates.Clear, result.IncidentState);
    }

    [Theory]
    [InlineData(ReportStatusCodes.Pending)]
    [InlineData(ReportStatusCodes.UnderReview)]
    [InlineData(ReportStatusCodes.Rejected)]
    [InlineData(ReportStatusCodes.Resolved)]
    [InlineData(ReportStatusCodes.Duplicate)]
    [InlineData(ReportStatusCodes.NeedsInfo)]
    public async Task NonVerifiedStatuses_NeverAffectRoute(string statusCode)
    {
        using var ctx = new ReportTestContext();
        AddAccident(ctx, 110, statusCode, isPublic: true, severityId: 4, latitude: 23.7501);

        var result = await CreateService(ctx).AssessRouteAsync(Route);

        Assert.Equal(RouteIncidentStates.Clear, result.IncidentState);
        Assert.Empty(result.Incidents);
    }

    [Fact]
    public async Task VerifiedPrivateReport_DoesNotLeakThroughRouting()
    {
        using var ctx = new ReportTestContext();
        AddAccident(ctx, 120, ReportStatusCodes.Verified, isPublic: false, severityId: 4, latitude: 23.7501);

        var result = await CreateService(ctx).AssessRouteAsync(Route);

        Assert.Empty(result.Incidents);
        Assert.Equal(RouteIncidentStates.Clear, result.IncidentState);
    }

    [Theory]
    [InlineData("Minor", RouteIncidentStates.Caution)]
    [InlineData("Moderate", RouteIncidentStates.Caution)]
    [InlineData("Severe", RouteIncidentStates.Affected)]
    [InlineData("Fatal", RouteIncidentStates.Affected)]
    public void AccidentSeverityPolicy_IsDeterministic(string severity, string expected)
    {
        Assert.Equal(expected, RouteIncidentPolicy.ClassifyAccident(severity));
    }

    [Theory]
    [InlineData("Pothole", "LOW", RouteIncidentStates.Caution)]
    [InlineData("Pothole", "MODERATE", RouteIncidentStates.Caution)]
    [InlineData("Pothole", "HIGH", RouteIncidentStates.Caution)]
    [InlineData("Pothole", "CRITICAL", RouteIncidentStates.Affected)]
    [InlineData("Road Block", "HIGH", RouteIncidentStates.Affected)]
    [InlineData("Road Block", "CRITICAL", RouteIncidentStates.Affected)]
    public void HazardRiskPolicy_IsDeterministic(string type, string risk, string expected)
    {
        Assert.Equal(expected, RouteIncidentPolicy.ClassifyHazard(type, risk));
    }

    [Fact]
    public async Task HighRoadBlock_IsAffectedWhileHighPotholeIsCaution()
    {
        using var ctx = new ReportTestContext();
        AddHazard(ctx, 130, 1, HazardRiskLevels.High, latitude: 23.7501);
        AddHazard(ctx, 131, 2, HazardRiskLevels.High, latitude: 23.75012);

        var result = await CreateService(ctx).AssessRouteAsync(Route);

        Assert.Equal(RouteIncidentStates.Affected, result.IncidentState);
        Assert.Contains(result.Incidents, i => i.HazardType == "Pothole" && i.IncidentLevel == RouteIncidentStates.Caution);
        Assert.Contains(result.Incidents, i => i.HazardType == "Road Block" && i.IncidentLevel == RouteIncidentStates.Affected);
    }

    private static RouteIncidentService CreateService(ReportTestContext ctx) =>
        new(
            ctx.Db,
            Options.Create(new OsrmRoutingOptions { IncidentProximityMeters = 50 }),
            NullLogger<RouteIncidentService>.Instance);

    private static void AddAccident(
        ReportTestContext ctx,
        ulong reportId,
        string statusCode,
        bool isPublic,
        byte severityId,
        double latitude)
    {
        var location = AddLocation(ctx, reportId, latitude, 90.3750);
        var status = ctx.Db.ReportStatuses.Single(s => s.StatusCode == statusCode);
        ctx.Db.Reports.Add(new Reports
        {
            ReportId = reportId,
            ReportType = ReportTypes.Accident,
            UserId = 7,
            LocationId = location.LocationId,
            StatusId = status.StatusId,
            Title = "Route accident",
            Description = "Not exposed by routing.",
            IsPublic = isPublic,
            ReportedAt = DateTime.UtcNow.AddMinutes(-5),
            UpdatedAt = DateTime.UtcNow
        });
        ctx.Db.AccidentReports.Add(new AccidentReports
        {
            ReportId = reportId,
            AccidentTypeId = 1,
            SeverityId = severityId,
            NumberOfInjured = 0,
            NumberOfDeaths = 0
        });
        ctx.Db.SaveChanges();
    }

    private static void AddHazard(
        ReportTestContext ctx,
        ulong reportId,
        ushort hazardTypeId,
        string risk,
        double latitude)
    {
        var location = AddLocation(ctx, reportId, latitude, 90.3752);
        var status = ctx.Db.ReportStatuses.Single(s => s.StatusCode == ReportStatusCodes.Verified);
        ctx.Db.Reports.Add(new Reports
        {
            ReportId = reportId,
            ReportType = ReportTypes.Hazard,
            UserId = 7,
            LocationId = location.LocationId,
            StatusId = status.StatusId,
            Title = "Route hazard",
            IsPublic = true,
            ReportedAt = DateTime.UtcNow.AddMinutes(-4),
            UpdatedAt = DateTime.UtcNow
        });
        ctx.Db.HazardReports.Add(new HazardReports
        {
            ReportId = reportId,
            HazardTypeId = hazardTypeId,
            RiskLevel = risk
        });
        ctx.Db.SaveChanges();
    }

    private static Locations AddLocation(ReportTestContext ctx, ulong reportId, double lat, double lng)
    {
        var location = new Locations
        {
            LocationId = 1000 + reportId,
            Latitude = (decimal)lat,
            Longitude = (decimal)lng,
            AreaName = "Test Area",
            City = "Dhaka",
            Country = "Bangladesh",
            PlaceProvider = "TEST",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        ctx.Db.Locations.Add(location);
        ctx.Db.SaveChanges();
        return location;
    }
}
