using Microsoft.EntityFrameworkCore;
using SafePathBD.Web.Common;
using SafePathBD.Web.Models.DTOs.Reports;
using SafePathBD.Web.Services.Interfaces;

namespace SafePathBD.Tests;

public class ReportCreationTests
{
    private const ulong ReporterId = 7;

    private static readonly ReportLocationInput Dhanmondi =
        new(23.7465, 90.3760, "Dhanmondi 27", null, "Dhanmondi", "Dhaka", "Dhaka", "OSM");

    private static CreateAccidentReportRequest Accident(
        ushort typeId = 1,
        byte severityId = 2,
        ReportLocationInput? location = null) =>
        new(ReporterId, "Two-car collision", "Description", location ?? Dhanmondi, typeId, severityId,
            DateTime.Now.AddHours(-1), 2, 1, 0, "Clear");

    private static CreateHazardReportRequest Hazard(
        ushort typeId = 1,
        string risk = HazardRiskLevels.High,
        ReportLocationInput? location = null) =>
        new(ReporterId, "Deep pothole in the left lane", "Description", location ?? Dhanmondi, typeId, risk,
            DateTime.Now.AddHours(-2), null);

    // ------------------------------------------------------------------ accident

    [Fact]
    public async Task CreateAccident_WritesParentAndSubtypeRows()
    {
        using var ctx = new ReportTestContext();

        var result = await ctx.Accidents.CreateAsync(Accident(), Array.Empty<StoredImage>());

        Assert.True(result.Succeeded);
        Assert.Equal(1, await ctx.Db.Reports.CountAsync());
        Assert.Equal(1, await ctx.Db.AccidentReports.CountAsync());
        Assert.Equal(0, await ctx.Db.HazardReports.CountAsync());

        var report = await ctx.Db.Reports.SingleAsync();
        Assert.Equal(ReportTypes.Accident, report.ReportType);
        Assert.Equal(ReporterId, report.UserId);
        Assert.Null(report.RoadSegmentId);
    }

    [Fact]
    public async Task CreateAccident_StartsAsPending()
    {
        using var ctx = new ReportTestContext();

        var result = await ctx.Accidents.CreateAsync(Accident(), Array.Empty<StoredImage>());

        var statusCode = await ctx.Db.Reports
            .Where(r => r.ReportId == result.ReportId)
            .Select(r => r.Status.StatusCode)
            .SingleAsync();

        Assert.Equal(ReportStatusCodes.Pending, statusCode);
    }

    [Fact]
    public async Task CreateAccident_RejectsAnUnknownAccidentType()
    {
        using var ctx = new ReportTestContext();

        var result = await ctx.Accidents.CreateAsync(Accident(typeId: 999), Array.Empty<StoredImage>());

        Assert.Equal(CreateReportStatus.InvalidLookup, result.Status);
        Assert.Equal(0, await ctx.Db.Reports.CountAsync());
    }

    [Fact]
    public async Task CreateAccident_RejectsAnUnknownSeverity()
    {
        using var ctx = new ReportTestContext();

        var result = await ctx.Accidents.CreateAsync(Accident(severityId: 99), Array.Empty<StoredImage>());

        Assert.Equal(CreateReportStatus.InvalidLookup, result.Status);
        Assert.Equal(0, await ctx.Db.Reports.CountAsync());
    }

    [Fact]
    public async Task CreateAccident_RejectsInvalidCoordinates()
    {
        using var ctx = new ReportTestContext();

        var result = await ctx.Accidents.CreateAsync(
            Accident(location: new ReportLocationInput(120, 400)), Array.Empty<StoredImage>());

        Assert.Equal(CreateReportStatus.InvalidLocation, result.Status);
        Assert.Equal(0, await ctx.Db.Locations.CountAsync());
    }

    [Fact]
    public async Task CreateAccident_FailsWhenThePendingStatusIsMissing()
    {
        using var ctx = new ReportTestContext(seedStatuses: false);

        var result = await ctx.Accidents.CreateAsync(Accident(), Array.Empty<StoredImage>());

        Assert.Equal(CreateReportStatus.StatusConfigurationMissing, result.Status);
        Assert.Equal(0, await ctx.Db.Reports.CountAsync());
    }

    [Fact]
    public async Task CreateAccident_StoresImageMetadata()
    {
        using var ctx = new ReportTestContext();

        var images = new[]
        {
            new StoredImage("2026/09/report-a.jpg", "C:/tmp/a.jpg", "a.jpg"),
            new StoredImage("2026/09/report-b.png", "C:/tmp/b.png", "b.png")
        };

        var result = await ctx.Accidents.CreateAsync(Accident(), images);

        var stored = await ctx.Db.ReportImages.Where(i => i.ReportId == result.ReportId).ToListAsync();
        Assert.Equal(2, stored.Count);
        Assert.Equal(
            new[] { "2026/09/report-a.jpg", "2026/09/report-b.png" },
            stored.Select(i => i.ImageUrl).OrderBy(u => u).ToArray());
    }

    // -------------------------------------------------------------------- hazard

    [Fact]
    public async Task CreateHazard_WritesParentAndSubtypeRows()
    {
        using var ctx = new ReportTestContext();

        var result = await ctx.Hazards.CreateAsync(Hazard(), Array.Empty<StoredImage>());

        Assert.True(result.Succeeded);
        Assert.Equal(1, await ctx.Db.Reports.CountAsync());
        Assert.Equal(1, await ctx.Db.HazardReports.CountAsync());
        Assert.Equal(0, await ctx.Db.AccidentReports.CountAsync());

        var report = await ctx.Db.Reports.SingleAsync();
        Assert.Equal(ReportTypes.Hazard, report.ReportType);
    }

    [Fact]
    public async Task CreateHazard_StartsAsPending()
    {
        using var ctx = new ReportTestContext();

        var result = await ctx.Hazards.CreateAsync(Hazard(), Array.Empty<StoredImage>());

        var statusCode = await ctx.Db.Reports
            .Where(r => r.ReportId == result.ReportId)
            .Select(r => r.Status.StatusCode)
            .SingleAsync();

        Assert.Equal(ReportStatusCodes.Pending, statusCode);
    }

    [Fact]
    public async Task CreateHazard_KeepsTheChosenRiskLevel()
    {
        using var ctx = new ReportTestContext();

        var result = await ctx.Hazards.CreateAsync(Hazard(risk: HazardRiskLevels.Critical), Array.Empty<StoredImage>());

        var risk = await ctx.Db.HazardReports
            .Where(h => h.ReportId == result.ReportId)
            .Select(h => h.RiskLevel)
            .SingleAsync();

        Assert.Equal(HazardRiskLevels.Critical, risk);
    }

    [Fact]
    public async Task CreateHazard_RejectsAnUnknownHazardType()
    {
        using var ctx = new ReportTestContext();

        var result = await ctx.Hazards.CreateAsync(Hazard(typeId: 999), Array.Empty<StoredImage>());

        Assert.Equal(CreateReportStatus.InvalidLookup, result.Status);
        Assert.Equal(0, await ctx.Db.Reports.CountAsync());
    }

    [Theory]
    [InlineData("EXTREME")]
    [InlineData("low")]
    [InlineData("")]
    public async Task CreateHazard_RejectsAnInvalidRiskLevel(string risk)
    {
        using var ctx = new ReportTestContext();

        var result = await ctx.Hazards.CreateAsync(Hazard(risk: risk), Array.Empty<StoredImage>());

        Assert.Equal(CreateReportStatus.InvalidLookup, result.Status);
        Assert.Equal(0, await ctx.Db.Reports.CountAsync());
    }

    // ------------------------------------------------------------------ location

    [Fact]
    public async Task CreateReports_ReuseTheLocationRowForTheSamePoint()
    {
        using var ctx = new ReportTestContext();

        await ctx.Accidents.CreateAsync(Accident(), Array.Empty<StoredImage>());
        await ctx.Hazards.CreateAsync(Hazard(), Array.Empty<StoredImage>());

        Assert.Equal(1, await ctx.Db.Locations.CountAsync());
    }

    [Fact]
    public async Task CreateReports_KeepDistinctPointsApart()
    {
        using var ctx = new ReportTestContext();

        await ctx.Accidents.CreateAsync(Accident(), Array.Empty<StoredImage>());
        await ctx.Hazards.CreateAsync(
            Hazard(location: new ReportLocationInput(23.8103, 90.4125, Provider: "OSM")),
            Array.Empty<StoredImage>());

        Assert.Equal(2, await ctx.Db.Locations.CountAsync());
    }
}
