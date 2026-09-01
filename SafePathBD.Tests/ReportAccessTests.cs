using SafePathBD.Web.Common;
using SafePathBD.Web.Models.DTOs.Reports;
using SafePathBD.Web.Services.Interfaces;

namespace SafePathBD.Tests;

public class ReportAccessTests
{
    private const ulong Owner = 7;
    private const ulong Stranger = 8;

    private static readonly ReportLocationInput Point = new(23.7465, 90.3760, "Dhanmondi 27", null, "Dhanmondi", "Dhaka", "Dhaka", "OSM");

    private static readonly MapBounds Dhaka = new(23.6, 90.2, 23.95, 90.6);

    private static async Task<ulong> AddHazardAsync(ReportTestContext ctx, ulong userId, string title = "Pothole")
    {
        var result = await ctx.Hazards.CreateAsync(
            new CreateHazardReportRequest(userId, title, null, Point, 1, HazardRiskLevels.High, DateTime.Now, null),
            Array.Empty<StoredImage>());

        return result.ReportId;
    }

    // ----------------------------------------------------------------- ownership

    [Fact]
    public async Task MyReports_ReturnsOnlyTheCallersReports()
    {
        using var ctx = new ReportTestContext();
        await AddHazardAsync(ctx, Owner, "Mine");
        await AddHazardAsync(ctx, Stranger, "Theirs");

        var mine = await ctx.Reports.GetMyReportsAsync(new MyReportsQuery(Owner));

        Assert.Single(mine.Items);
        Assert.Equal("Mine", mine.Items[0].Title);
    }

    [Fact]
    public async Task MyReports_FiltersByReportType()
    {
        using var ctx = new ReportTestContext();
        await AddHazardAsync(ctx, Owner);
        await ctx.Accidents.CreateAsync(
            new CreateAccidentReportRequest(Owner, "Collision", null, Point, 1, 2, DateTime.Now, 2, 0, 0, null),
            Array.Empty<StoredImage>());

        var accidents = await ctx.Reports.GetMyReportsAsync(new MyReportsQuery(Owner, ReportType: ReportTypes.Accident));

        Assert.Single(accidents.Items);
        Assert.Equal(ReportTypes.Accident, accidents.Items[0].ReportType);
    }

    [Fact]
    public async Task MyReports_FiltersByStatus()
    {
        using var ctx = new ReportTestContext();
        var verifiedId = await AddHazardAsync(ctx, Owner, "Verified one");
        await AddHazardAsync(ctx, Owner, "Still pending");
        ctx.SetStatus(verifiedId, ReportStatusCodes.Verified);

        var verified = await ctx.Reports.GetMyReportsAsync(new MyReportsQuery(Owner, StatusCode: ReportStatusCodes.Verified));

        Assert.Single(verified.Items);
        Assert.Equal("Verified one", verified.Items[0].Title);
    }

    [Fact]
    public async Task MyReports_PaginatesResults()
    {
        using var ctx = new ReportTestContext();
        for (var i = 0; i < 5; i++)
        {
            await AddHazardAsync(ctx, Owner, "Hazard " + i);
        }

        var page = await ctx.Reports.GetMyReportsAsync(new MyReportsQuery(Owner, Page: 2, PageSize: 2));

        Assert.Equal(2, page.Items.Count);
        Assert.Equal(5, page.TotalCount);
        Assert.Equal(3, page.TotalPages);
        Assert.True(page.HasPrevious);
        Assert.True(page.HasNext);
    }

    [Fact]
    public async Task MyReportStats_CountsByStatus()
    {
        using var ctx = new ReportTestContext();
        var verifiedId = await AddHazardAsync(ctx, Owner);
        await AddHazardAsync(ctx, Owner);
        ctx.SetStatus(verifiedId, ReportStatusCodes.Verified);

        var stats = await ctx.Reports.GetMyReportStatsAsync(Owner);

        Assert.Equal(2, stats.Total);
        Assert.Equal(1, stats.Pending);
        Assert.Equal(1, stats.Verified);
    }

    // ------------------------------------------------------------- detail access

    [Fact]
    public async Task Details_AreVisibleToTheOwnerWhilePending()
    {
        using var ctx = new ReportTestContext();
        var id = await AddHazardAsync(ctx, Owner);

        var details = await ctx.Reports.GetDetailsAsync(id, Owner, viewerIsStaff: false);

        Assert.NotNull(details);
        Assert.True(details!.IsOwnedByViewer);
    }

    [Fact]
    public async Task Details_AreHiddenFromOtherUsersWhilePending()
    {
        using var ctx = new ReportTestContext();
        var id = await AddHazardAsync(ctx, Owner);

        Assert.Null(await ctx.Reports.GetDetailsAsync(id, Stranger, viewerIsStaff: false));
        Assert.Null(await ctx.Reports.GetDetailsAsync(id, null, viewerIsStaff: false));
    }

    [Fact]
    public async Task Details_AreVisibleToStaffWhilePending()
    {
        using var ctx = new ReportTestContext();
        var id = await AddHazardAsync(ctx, Owner);

        var details = await ctx.Reports.GetDetailsAsync(id, Stranger, viewerIsStaff: true);

        Assert.NotNull(details);
        Assert.False(details!.IsOwnedByViewer);
    }

    [Fact]
    public async Task Details_BecomePublicOnceVerifiedAndPublic()
    {
        using var ctx = new ReportTestContext();
        var id = await AddHazardAsync(ctx, Owner);
        ctx.SetStatus(id, ReportStatusCodes.Verified);

        Assert.NotNull(await ctx.Reports.GetDetailsAsync(id, null, viewerIsStaff: false));
    }

    [Fact]
    public async Task Details_StayHiddenWhenVerifiedButNotPublic()
    {
        using var ctx = new ReportTestContext();
        var id = await AddHazardAsync(ctx, Owner);
        ctx.SetStatus(id, ReportStatusCodes.Verified, isPublic: false);

        Assert.Null(await ctx.Reports.GetDetailsAsync(id, null, viewerIsStaff: false));
    }

    [Fact]
    public async Task Details_ReturnNullForAMissingReport()
    {
        using var ctx = new ReportTestContext();

        Assert.Null(await ctx.Reports.GetDetailsAsync(4242, Owner, viewerIsStaff: true));
    }

    [Fact]
    public async Task Details_HideTheReporterNameFromThePublic()
    {
        using var ctx = new ReportTestContext();
        var id = await AddHazardAsync(ctx, Owner);
        ctx.SetStatus(id, ReportStatusCodes.Verified);

        var anonymous = await ctx.Reports.GetDetailsAsync(id, null, viewerIsStaff: false);
        var owner = await ctx.Reports.GetDetailsAsync(id, Owner, viewerIsStaff: false);
        var staff = await ctx.Reports.GetDetailsAsync(id, Stranger, viewerIsStaff: true);

        Assert.Equal("Community reporter", anonymous!.ReporterName);
        Assert.Equal("Reporter One", owner!.ReporterName);
        Assert.Equal("Reporter One", staff!.ReporterName);
    }

    // ------------------------------------------------------------ image access

    private static async Task<ulong> AddHazardWithImageAsync(ReportTestContext ctx, ulong userId)
    {
        var result = await ctx.Hazards.CreateAsync(
            new CreateHazardReportRequest(userId, "Pothole with photo", null, Point, 1, HazardRiskLevels.High, DateTime.Now, null),
            new[] { new StoredImage("2026/09/report-abc.png", "C:/tmp/report-abc.png", "photo.png") });

        return result.ReportId;
    }

    [Fact]
    public async Task Images_AreVisibleToTheOwnerWhilePending()
    {
        using var ctx = new ReportTestContext();
        await AddHazardWithImageAsync(ctx, Owner);
        var imageId = ctx.Db.ReportImages.Single().ImageId;

        var file = await ctx.Reports.GetImageForViewerAsync(imageId, Owner, viewerIsStaff: false);

        Assert.NotNull(file);
        Assert.Equal("2026/09/report-abc.png", file!.StorageKey);
    }

    [Fact]
    public async Task Images_AreHiddenFromOthersWhilePending()
    {
        using var ctx = new ReportTestContext();
        await AddHazardWithImageAsync(ctx, Owner);
        var imageId = ctx.Db.ReportImages.Single().ImageId;

        Assert.Null(await ctx.Reports.GetImageForViewerAsync(imageId, Stranger, viewerIsStaff: false));
        Assert.Null(await ctx.Reports.GetImageForViewerAsync(imageId, null, viewerIsStaff: false));
    }

    [Fact]
    public async Task Images_AreVisibleToStaffWhilePending()
    {
        using var ctx = new ReportTestContext();
        await AddHazardWithImageAsync(ctx, Owner);
        var imageId = ctx.Db.ReportImages.Single().ImageId;

        Assert.NotNull(await ctx.Reports.GetImageForViewerAsync(imageId, Stranger, viewerIsStaff: true));
    }

    [Fact]
    public async Task Images_BecomePublicOnceTheReportIsVerifiedAndPublic()
    {
        using var ctx = new ReportTestContext();
        var reportId = await AddHazardWithImageAsync(ctx, Owner);
        var imageId = ctx.Db.ReportImages.Single().ImageId;
        ctx.SetStatus(reportId, ReportStatusCodes.Verified);

        Assert.NotNull(await ctx.Reports.GetImageForViewerAsync(imageId, null, viewerIsStaff: false));
    }

    [Fact]
    public async Task Images_StayHiddenWhenTheReportIsVerifiedButNotPublic()
    {
        using var ctx = new ReportTestContext();
        var reportId = await AddHazardWithImageAsync(ctx, Owner);
        var imageId = ctx.Db.ReportImages.Single().ImageId;
        ctx.SetStatus(reportId, ReportStatusCodes.Verified, isPublic: false);

        Assert.Null(await ctx.Reports.GetImageForViewerAsync(imageId, null, viewerIsStaff: false));
    }

    [Fact]
    public async Task Images_ReturnNullForAMissingImage()
    {
        using var ctx = new ReportTestContext();

        Assert.Null(await ctx.Reports.GetImageForViewerAsync(9999, Owner, viewerIsStaff: true));
    }

    // ---------------------------------------------------------------- map layers

    [Fact]
    public async Task PublicMap_ExcludesPendingReports()
    {
        using var ctx = new ReportTestContext();
        await AddHazardAsync(ctx, Owner);

        Assert.Empty(await ctx.Reports.GetPublicMapReportsAsync(Dhaka, null, 100));
    }

    [Fact]
    public async Task PublicMap_ExcludesRejectedReports()
    {
        using var ctx = new ReportTestContext();
        var id = await AddHazardAsync(ctx, Owner);
        ctx.SetStatus(id, ReportStatusCodes.Rejected);

        Assert.Empty(await ctx.Reports.GetPublicMapReportsAsync(Dhaka, null, 100));
    }

    [Fact]
    public async Task PublicMap_ExcludesVerifiedButPrivateReports()
    {
        using var ctx = new ReportTestContext();
        var id = await AddHazardAsync(ctx, Owner);
        ctx.SetStatus(id, ReportStatusCodes.Verified, isPublic: false);

        Assert.Empty(await ctx.Reports.GetPublicMapReportsAsync(Dhaka, null, 100));
    }

    [Fact]
    public async Task PublicMap_IncludesVerifiedPublicReports()
    {
        using var ctx = new ReportTestContext();
        var id = await AddHazardAsync(ctx, Owner);
        ctx.SetStatus(id, ReportStatusCodes.Verified);

        var markers = await ctx.Reports.GetPublicMapReportsAsync(Dhaka, null, 100);

        Assert.Single(markers);
        Assert.Equal(ReportStatusCodes.Verified, markers[0].StatusCode);
        Assert.Equal(HazardRiskLevels.High, markers[0].RiskLevel);
    }

    [Fact]
    public async Task PublicMap_ExcludesReportsOutsideTheBounds()
    {
        using var ctx = new ReportTestContext();
        var id = await AddHazardAsync(ctx, Owner);
        ctx.SetStatus(id, ReportStatusCodes.Verified);

        var elsewhere = new MapBounds(10, 10, 11, 11);

        Assert.Empty(await ctx.Reports.GetPublicMapReportsAsync(elsewhere, null, 100));
    }

    [Fact]
    public async Task PublicMap_FiltersByReportType()
    {
        using var ctx = new ReportTestContext();
        var id = await AddHazardAsync(ctx, Owner);
        ctx.SetStatus(id, ReportStatusCodes.Verified);

        Assert.Empty(await ctx.Reports.GetPublicMapReportsAsync(Dhaka, ReportTypes.Accident, 100));
        Assert.Single(await ctx.Reports.GetPublicMapReportsAsync(Dhaka, ReportTypes.Hazard, 100));
    }

    [Theory]
    [InlineData(23.6, 90.2, 23.9, 90.6, true)]
    [InlineData(23.9, 90.2, 23.6, 90.6, false)]
    [InlineData(-91, 90.2, 23.9, 90.6, false)]
    [InlineData(23.6, 90.2, 23.9, 181, false)]
    public void MapBounds_ValidatesRangeAndOrdering(double minLat, double minLng, double maxLat, double maxLng, bool expected)
    {
        Assert.Equal(expected, new MapBounds(minLat, minLng, maxLat, maxLng).IsValid);
    }
}
