using Microsoft.EntityFrameworkCore;
using SafePathBD.Web.Common;
using SafePathBD.Web.Models.DTOs.Moderation;
using SafePathBD.Web.Models.DTOs.Reports;
using SafePathBD.Web.Services.Interfaces;

namespace SafePathBD.Tests;

/// <summary>
/// Status workflow, audit trail and promotion of verified accidents into the trusted dataset.
/// </summary>
public class ReportModerationTests
{
    private const ulong Reporter = 7;
    private const ulong Moderator = 9;

    private static readonly ReportLocationInput Point =
        new(23.7465, 90.3760, "Dhanmondi 27", null, "Dhanmondi", "Dhaka", "Dhaka", "OSM");

    private static Task<CreateReportResult> AddAccidentAsync(ReportTestContext ctx, DateTime? occurredAt = null) =>
        ctx.Accidents.CreateAsync(
            new CreateAccidentReportRequest(
                Reporter, "Collision at the junction", "Two cars.", Point,
                1, 2, occurredAt, 2, 1, 0, "Heavy rain"),
            Array.Empty<StoredImage>());

    private static Task<CreateReportResult> AddHazardAsync(ReportTestContext ctx) =>
        ctx.Hazards.CreateAsync(
            new CreateHazardReportRequest(Reporter, "Pothole", null, Point, 1, HazardRiskLevels.High, DateTime.Now, null),
            Array.Empty<StoredImage>());

    private static ModerationDecision Decide(ulong reportId, string target, string? note = null, string? expected = null) =>
        new(reportId, Moderator, target, note, expected);

    // -------------------------------------------------------------- transitions

    [Theory]
    [InlineData(ReportStatusCodes.Pending, ReportStatusCodes.UnderReview)]
    [InlineData(ReportStatusCodes.Pending, ReportStatusCodes.Verified)]
    [InlineData(ReportStatusCodes.Pending, ReportStatusCodes.Rejected)]
    [InlineData(ReportStatusCodes.Pending, ReportStatusCodes.Duplicate)]
    [InlineData(ReportStatusCodes.Pending, ReportStatusCodes.NeedsInfo)]
    [InlineData(ReportStatusCodes.UnderReview, ReportStatusCodes.Verified)]
    [InlineData(ReportStatusCodes.NeedsInfo, ReportStatusCodes.Verified)]
    [InlineData(ReportStatusCodes.Verified, ReportStatusCodes.Resolved)]
    public void AllowedTransitionsAreAccepted(string from, string to)
    {
        Assert.True(ReportStatusTransitions.IsAllowed(from, to));
    }

    [Theory]
    [InlineData(ReportStatusCodes.Rejected, ReportStatusCodes.Verified)]
    [InlineData(ReportStatusCodes.Resolved, ReportStatusCodes.Pending)]
    [InlineData(ReportStatusCodes.Duplicate, ReportStatusCodes.Verified)]
    [InlineData(ReportStatusCodes.Pending, ReportStatusCodes.Resolved)]
    [InlineData(ReportStatusCodes.Verified, ReportStatusCodes.Rejected)]
    public void ForbiddenTransitionsAreRefused(string from, string to)
    {
        Assert.False(ReportStatusTransitions.IsAllowed(from, to));
    }

    [Fact]
    public async Task AnInvalidTransitionChangesNothing()
    {
        using var ctx = new ReportTestContext();
        var report = await AddHazardAsync(ctx);

        var result = await ctx.Moderation.ApplyDecisionAsync(Decide(report.ReportId, ReportStatusCodes.Resolved));

        Assert.False(result.Succeeded);
        Assert.Equal(ModerationStatus.InvalidTransition, result.Status);
        Assert.Equal(0, await ctx.Db.ReportVerifications.CountAsync());
        Assert.Equal(0, await ctx.Db.AdminActions.CountAsync());
    }

    [Theory]
    [InlineData(ReportStatusCodes.Rejected)]
    [InlineData(ReportStatusCodes.Duplicate)]
    [InlineData(ReportStatusCodes.NeedsInfo)]
    public async Task DecisionsThatAffectTheReporterRequireANote(string target)
    {
        using var ctx = new ReportTestContext();
        var report = await AddHazardAsync(ctx);

        var result = await ctx.Moderation.ApplyDecisionAsync(Decide(report.ReportId, target));

        Assert.False(result.Succeeded);
        Assert.Equal(ModerationStatus.NoteRequired, result.Status);
        Assert.Equal(0, await ctx.Db.ReportVerifications.CountAsync());
    }

    [Fact]
    public async Task AStaleDecisionIsRefusedRatherThanOverwritingAnotherReviewer()
    {
        using var ctx = new ReportTestContext();
        var report = await AddHazardAsync(ctx);

        // Reviewer A acts first.
        await ctx.Moderation.ApplyDecisionAsync(Decide(report.ReportId, ReportStatusCodes.UnderReview, expected: ReportStatusCodes.Pending));

        // Reviewer B still had the PENDING page open.
        var stale = await ctx.Moderation.ApplyDecisionAsync(
            Decide(report.ReportId, ReportStatusCodes.Verified, expected: ReportStatusCodes.Pending));

        Assert.False(stale.Succeeded);
        Assert.Equal(ModerationStatus.StaleState, stale.Status);
        Assert.Equal(1, await ctx.Db.ReportVerifications.CountAsync());
    }

    [Fact]
    public async Task AMissingReportIsReportedCleanly()
    {
        using var ctx = new ReportTestContext();

        var result = await ctx.Moderation.ApplyDecisionAsync(Decide(9999, ReportStatusCodes.Verified));

        Assert.Equal(ModerationStatus.NotFound, result.Status);
    }

    // ------------------------------------------------------------------ verify

    [Fact]
    public async Task VerifyingAnAccidentWritesHistoryStatusAuditAndTrustedAccident()
    {
        using var ctx = new ReportTestContext();
        var report = await AddAccidentAsync(ctx, new DateTime(2026, 8, 30, 14, 15, 0));

        var result = await ctx.Moderation.ApplyDecisionAsync(
            Decide(report.ReportId, ReportStatusCodes.Verified, "Location and evidence confirmed."));

        Assert.True(result.Succeeded);
        Assert.Equal(ReportStatusCodes.Verified, result.NewStatusCode);

        var stored = await ctx.Db.Reports.Include(r => r.Status).SingleAsync(r => r.ReportId == report.ReportId);
        Assert.Equal(ReportStatusCodes.Verified, stored.Status.StatusCode);

        var verification = await ctx.Db.ReportVerifications.SingleAsync();
        Assert.Equal(Moderator, verification.AdminUserId);
        Assert.Equal("Location and evidence confirmed.", verification.AdminComment);

        var accident = await ctx.Db.Accidents.SingleAsync();
        Assert.Equal(report.ReportId, accident.SourceReportId);
        Assert.Equal(new DateTime(2026, 8, 30, 14, 15, 0), accident.AccidentOccurredAt);
        Assert.Equal(Moderator, accident.VerifiedBy);
        Assert.Equal((ushort)2, accident.NumberOfVehicles);
        Assert.Equal((ushort)1, accident.NumberOfInjured);
        Assert.Equal("Heavy rain", accident.WeatherCondition);

        // One audit row for the status change and one for the promotion.
        Assert.Equal(2, await ctx.Db.AdminActions.CountAsync());
        Assert.Contains(ctx.Db.AdminActions, a => a.ActionType == AdminActionTypes.ReportVerified);
        Assert.Contains(ctx.Db.AdminActions, a => a.ActionType == AdminActionTypes.AccidentPromoted);
    }

    [Fact]
    public async Task AnAccidentWithNoStatedTimeFallsBackToWhenItWasReported()
    {
        using var ctx = new ReportTestContext();
        var report = await AddAccidentAsync(ctx, occurredAt: null);

        await ctx.Moderation.ApplyDecisionAsync(Decide(report.ReportId, ReportStatusCodes.Verified));

        var reported = ctx.Db.Reports.Single(r => r.ReportId == report.ReportId).ReportedAt;
        var accident = await ctx.Db.Accidents.SingleAsync();

        // accidents.accident_occurred_at is NOT NULL, so it must never be left unset.
        Assert.Equal(reported, accident.AccidentOccurredAt);
    }

    [Fact]
    public async Task AnAccidentIsNeverPromotedTwice()
    {
        using var ctx = new ReportTestContext();
        var report = await AddAccidentAsync(ctx);

        await ctx.Moderation.ApplyDecisionAsync(Decide(report.ReportId, ReportStatusCodes.Verified));

        // A second verify is not a legal transition, and even so no extra trusted row appears.
        var second = await ctx.Moderation.ApplyDecisionAsync(Decide(report.ReportId, ReportStatusCodes.Verified));

        Assert.False(second.Succeeded);
        Assert.Equal(1, await ctx.Db.Accidents.CountAsync());
    }

    [Fact]
    public async Task ReVerifyingThroughAllowedPathsStillPromotesOnlyOnce()
    {
        using var ctx = new ReportTestContext();
        var report = await AddAccidentAsync(ctx);

        await ctx.Moderation.ApplyDecisionAsync(Decide(report.ReportId, ReportStatusCodes.Verified));
        await ctx.Moderation.ApplyDecisionAsync(Decide(report.ReportId, ReportStatusCodes.Resolved));

        Assert.Equal(1, await ctx.Db.Accidents.CountAsync());
    }

    [Fact]
    public async Task VerifyingAHazardNeverCreatesATrustedAccident()
    {
        using var ctx = new ReportTestContext();
        var report = await AddHazardAsync(ctx);

        var result = await ctx.Moderation.ApplyDecisionAsync(Decide(report.ReportId, ReportStatusCodes.Verified));

        Assert.True(result.Succeeded);
        Assert.Equal(1, await ctx.Db.ReportVerifications.CountAsync());
        Assert.Equal(0, await ctx.Db.Accidents.CountAsync());
        Assert.Equal(1, await ctx.Db.AdminActions.CountAsync());
    }

    // ---------------------------------------------------- reject / other paths

    [Fact]
    public async Task RejectingRecordsTheReasonAndClosesTheReport()
    {
        using var ctx = new ReportTestContext();
        var report = await AddHazardAsync(ctx);

        var result = await ctx.Moderation.ApplyDecisionAsync(
            Decide(report.ReportId, ReportStatusCodes.Rejected, "Insufficient evidence."));

        Assert.True(result.Succeeded);

        var verification = await ctx.Db.ReportVerifications.SingleAsync();
        Assert.Equal("Insufficient evidence.", verification.AdminComment);
        Assert.Equal(AdminActionTypes.ReportRejected, (await ctx.Db.AdminActions.SingleAsync()).ActionType);
    }

    [Fact]
    public async Task ResolvingStampsTheResolvedTime()
    {
        using var ctx = new ReportTestContext();
        var report = await AddHazardAsync(ctx);

        await ctx.Moderation.ApplyDecisionAsync(Decide(report.ReportId, ReportStatusCodes.Verified));
        await ctx.Moderation.ApplyDecisionAsync(Decide(report.ReportId, ReportStatusCodes.Resolved));

        var stored = ctx.Db.Reports.Single(r => r.ReportId == report.ReportId);
        Assert.NotNull(stored.ResolvedAt);
    }

    [Fact]
    public async Task EveryDecisionAppendsToHistoryAndNeverOverwritesIt()
    {
        using var ctx = new ReportTestContext();
        var report = await AddHazardAsync(ctx);

        await ctx.Moderation.ApplyDecisionAsync(Decide(report.ReportId, ReportStatusCodes.UnderReview));
        await ctx.Moderation.ApplyDecisionAsync(Decide(report.ReportId, ReportStatusCodes.NeedsInfo, "Please add a photo."));
        await ctx.Moderation.ApplyDecisionAsync(Decide(report.ReportId, ReportStatusCodes.Verified));

        var history = await ctx.Db.ReportVerifications.OrderBy(v => v.VerificationId).ToListAsync();

        Assert.Equal(3, history.Count);
        Assert.Equal(3, await ctx.Db.AdminActions.CountAsync());
    }

    // ------------------------------------------------------------------ queue

    [Fact]
    public async Task TheQueueCountsComeFromRealStatuses()
    {
        using var ctx = new ReportTestContext();
        await AddHazardAsync(ctx);
        var second = await AddHazardAsync(ctx);
        await ctx.Moderation.ApplyDecisionAsync(Decide(second.ReportId, ReportStatusCodes.Verified));

        var counts = await ctx.Moderation.GetCountsAsync();

        Assert.Equal(1, counts.Pending);
        Assert.Equal(1, counts.Verified);
        Assert.Equal(1, counts.OpenTotal);
    }

    [Fact]
    public async Task TheQueueFiltersByStatusAndType()
    {
        using var ctx = new ReportTestContext();
        await AddHazardAsync(ctx);
        await AddAccidentAsync(ctx);

        var hazards = await ctx.Moderation.GetQueueAsync(
            new ModerationQueueQuery(ReportStatusCodes.Pending, ReportTypes.Hazard));

        Assert.Single(hazards.Items);
        Assert.Equal(ReportTypes.Hazard, hazards.Items[0].ReportType);
    }

    [Fact]
    public async Task TheQueueCarriesAggregateVoteCounts()
    {
        using var ctx = new ReportTestContext();
        var report = await AddHazardAsync(ctx);
        ctx.SetStatus(report.ReportId, ReportStatusCodes.Verified);
        await ctx.Community.CastVoteAsync(report.ReportId, 8, ReportVoteTypes.Confirm);

        var queue = await ctx.Moderation.GetQueueAsync(new ModerationQueueQuery(ReportStatusCodes.Verified));

        Assert.Equal(1, queue.Items[0].ConfirmCount);
        Assert.Equal(0, queue.Items[0].DisputeCount);
    }

    [Fact]
    public async Task TheReviewProjectionExposesReporterIdentityAndAllowedNextSteps()
    {
        using var ctx = new ReportTestContext();
        var report = await AddHazardAsync(ctx);

        var review = await ctx.Moderation.GetForReviewAsync(report.ReportId);

        Assert.NotNull(review);
        Assert.Equal("Reporter One", review!.ReporterName);
        Assert.Equal("reporter@example.com", review.ReporterEmail);
        Assert.Contains(ReportStatusCodes.Verified, review.AllowedTransitions);
        Assert.DoesNotContain(ReportStatusCodes.Resolved, review.AllowedTransitions);
    }

    [Fact]
    public async Task AClosedReportOffersNoFurtherTransitions()
    {
        using var ctx = new ReportTestContext();
        var report = await AddHazardAsync(ctx);
        await ctx.Moderation.ApplyDecisionAsync(Decide(report.ReportId, ReportStatusCodes.Rejected, "Not road-safety related."));

        var review = await ctx.Moderation.GetForReviewAsync(report.ReportId);

        Assert.Empty(review!.AllowedTransitions);
    }
}
