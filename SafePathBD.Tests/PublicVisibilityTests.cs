using SafePathBD.Web.Common;
using SafePathBD.Web.Models.DTOs.Moderation;
using SafePathBD.Web.Models.DTOs.Reports;
using SafePathBD.Web.Services.Interfaces;

namespace SafePathBD.Tests;

/// <summary>
/// What the public map is allowed to show once moderation has run.
/// </summary>
public class PublicVisibilityTests
{
    private const ulong Reporter = 7;
    private const ulong Moderator = 9;

    private static readonly ReportLocationInput Point =
        new(23.7465, 90.3760, "Dhanmondi 27", null, "Dhanmondi", "Dhaka", "Dhaka", "OSM");

    private static readonly MapBounds Dhaka = new(23.6, 90.2, 23.95, 90.6);

    private static Task<CreateReportResult> AddHazardAsync(ReportTestContext ctx) =>
        ctx.Hazards.CreateAsync(
            new CreateHazardReportRequest(Reporter, "Pothole", null, Point, 1, HazardRiskLevels.High, DateTime.Now, null),
            Array.Empty<StoredImage>());

    [Fact]
    public async Task APendingPublicReportIsNotOnTheMap()
    {
        using var ctx = new ReportTestContext();
        await AddHazardAsync(ctx);

        var markers = await ctx.Reports.GetPublicMapReportsAsync(Dhaka, null, 100);

        Assert.Empty(markers);
    }

    [Fact]
    public async Task VerifyingAPublicReportPutsItOnTheMap()
    {
        using var ctx = new ReportTestContext();
        var report = await AddHazardAsync(ctx);

        await ctx.Moderation.ApplyDecisionAsync(
            new ModerationDecision(report.ReportId, Moderator, ReportStatusCodes.Verified, null, null));

        var markers = await ctx.Reports.GetPublicMapReportsAsync(Dhaka, null, 100);

        Assert.Single(markers);
        Assert.Equal(report.ReportId, markers[0].ReportId);
    }

    [Fact]
    public async Task ARejectedReportNeverReachesTheMap()
    {
        using var ctx = new ReportTestContext();
        var report = await AddHazardAsync(ctx);

        await ctx.Moderation.ApplyDecisionAsync(
            new ModerationDecision(report.ReportId, Moderator, ReportStatusCodes.Rejected, "False information.", null));

        Assert.Empty(await ctx.Reports.GetPublicMapReportsAsync(Dhaka, null, 100));
    }

    [Fact]
    public async Task ADuplicateReportNeverReachesTheMap()
    {
        using var ctx = new ReportTestContext();
        var report = await AddHazardAsync(ctx);

        await ctx.Moderation.ApplyDecisionAsync(
            new ModerationDecision(report.ReportId, Moderator, ReportStatusCodes.Duplicate, "Already reported as #1.", null));

        Assert.Empty(await ctx.Reports.GetPublicMapReportsAsync(Dhaka, null, 100));
    }

    [Fact]
    public async Task AVerifiedButPrivateReportStaysOffTheMap()
    {
        using var ctx = new ReportTestContext();
        var report = await AddHazardAsync(ctx);
        ctx.SetStatus(report.ReportId, ReportStatusCodes.Pending, isPublic: false);

        await ctx.Moderation.ApplyDecisionAsync(
            new ModerationDecision(report.ReportId, Moderator, ReportStatusCodes.Verified, null, null));

        Assert.Empty(await ctx.Reports.GetPublicMapReportsAsync(Dhaka, null, 100));
    }

    [Fact]
    public async Task MapMarkersCarryAggregateVotesButNoReporterIdentity()
    {
        using var ctx = new ReportTestContext();
        var report = await AddHazardAsync(ctx);
        await ctx.Moderation.ApplyDecisionAsync(
            new ModerationDecision(report.ReportId, Moderator, ReportStatusCodes.Verified, null, null));
        await ctx.Community.CastVoteAsync(report.ReportId, 8, ReportVoteTypes.Confirm);

        var marker = (await ctx.Reports.GetPublicMapReportsAsync(Dhaka, null, 100)).Single();

        Assert.Equal(1, marker.ConfirmCount);
        Assert.Equal(0, marker.DisputeCount);

        // The record simply has no field that could carry an identity.
        Assert.DoesNotContain(
            typeof(MapReportDto).GetProperties(),
            p => p.Name.Contains("User") || p.Name.Contains("Reporter") || p.Name.Contains("Email"));
    }

    [Fact]
    public async Task AVerifiedReportBecomesVisibleToAnonymousViewers()
    {
        using var ctx = new ReportTestContext();
        var report = await AddHazardAsync(ctx);

        Assert.Null(await ctx.Reports.GetDetailsAsync(report.ReportId, null, false));

        await ctx.Moderation.ApplyDecisionAsync(
            new ModerationDecision(report.ReportId, Moderator, ReportStatusCodes.Verified, null, null));

        var details = await ctx.Reports.GetDetailsAsync(report.ReportId, null, false);

        Assert.NotNull(details);
        Assert.Equal("Community reporter", details!.ReporterName);
    }

    [Fact]
    public async Task ReviewNotesAreHiddenFromThePublicButShownToTheOwner()
    {
        using var ctx = new ReportTestContext();
        var report = await AddHazardAsync(ctx);

        await ctx.Moderation.ApplyDecisionAsync(
            new ModerationDecision(report.ReportId, Moderator, ReportStatusCodes.NeedsInfo, "Please add a photo of the junction.", null));

        var publicHistory = await ctx.Reports.GetVerificationHistoryAsync(report.ReportId, includeNotes: false);
        var ownerHistory = await ctx.Reports.GetVerificationHistoryAsync(report.ReportId, includeNotes: true);

        Assert.Null(publicHistory.Single().Note);
        Assert.Equal("Please add a photo of the junction.", ownerHistory.Single().Note);

        // The status itself is not secret; only the internal note is.
        Assert.Equal(ReportStatusCodes.NeedsInfo, publicHistory.Single().StatusCode);
    }
}
