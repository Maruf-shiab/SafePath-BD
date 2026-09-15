using Microsoft.EntityFrameworkCore;
using SafePathBD.Web.Common;
using SafePathBD.Web.Models.DTOs.Reports;
using SafePathBD.Web.Services.Interfaces;

namespace SafePathBD.Tests;

/// <summary>Community confirm/dispute voting rules.</summary>
public class ReportVoteTests
{
    private const ulong Owner = 7;
    private const ulong Voter = 8;
    private const ulong Moderator = 9;

    private static readonly ReportLocationInput Point =
        new(23.7465, 90.3760, "Dhanmondi 27", null, "Dhanmondi", "Dhaka", "Dhaka", "OSM");

    /// <summary>A public hazard left PENDING — the state community review exists for.</summary>
    private static async Task<ulong> AddPendingHazardAsync(ReportTestContext ctx)
    {
        var result = await ctx.Hazards.CreateAsync(
            new CreateHazardReportRequest(Owner, "Pothole", null, Point, 1, HazardRiskLevels.High, DateTime.Now, null),
            Array.Empty<StoredImage>());

        return result.ReportId;
    }

    // ------------------------------------------------- pre-verification voting

    [Fact]
    public async Task AMemberCanConfirmAPendingReportBeforeAnyModeratorSeesIt()
    {
        using var ctx = new ReportTestContext();
        var reportId = await AddPendingHazardAsync(ctx);

        var result = await ctx.Community.CastVoteAsync(reportId, ReportTestContext.Member(Voter), ReportVoteTypes.Confirm);

        Assert.True(result.Succeeded);
        Assert.Equal(1, result.Data!.ConfirmCount);
        Assert.Equal(ReportVoteTypes.Confirm, result.Data.CurrentUserVote);
    }

    [Fact]
    public async Task AMemberCanDisputeAPendingReport()
    {
        using var ctx = new ReportTestContext();
        var reportId = await AddPendingHazardAsync(ctx);

        var result = await ctx.Community.CastVoteAsync(reportId, ReportTestContext.Member(Voter), ReportVoteTypes.Dispute);

        Assert.True(result.Succeeded);
        Assert.Equal(1, result.Data!.DisputeCount);
    }

    [Theory]
    [InlineData(ReportStatusCodes.Pending)]
    [InlineData(ReportStatusCodes.UnderReview)]
    [InlineData(ReportStatusCodes.NeedsInfo)]
    [InlineData(ReportStatusCodes.Verified)]
    public async Task EveryCommunityReviewableStageAcceptsVotes(string statusCode)
    {
        using var ctx = new ReportTestContext();
        var reportId = await AddPendingHazardAsync(ctx);
        ctx.SetStatus(reportId, statusCode);

        var result = await ctx.Community.CastVoteAsync(reportId, ReportTestContext.Member(Voter), ReportVoteTypes.Confirm);

        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData(ReportStatusCodes.Rejected)]
    [InlineData(ReportStatusCodes.Duplicate)]
    public async Task AClosedReportCannotBeVotedOn(string statusCode)
    {
        using var ctx = new ReportTestContext();
        var reportId = await AddPendingHazardAsync(ctx);
        ctx.SetStatus(reportId, statusCode);

        var result = await ctx.Community.CastVoteAsync(reportId, ReportTestContext.Member(Voter), ReportVoteTypes.Confirm);

        Assert.False(result.Succeeded);
        Assert.Equal(CommunityStatus.ReportNotFound, result.Status);
    }

    [Fact]
    public async Task APrivatePendingReportIsNotOpenToOtherMembers()
    {
        using var ctx = new ReportTestContext();
        var reportId = await AddPendingHazardAsync(ctx);
        ctx.SetStatus(reportId, ReportStatusCodes.Pending, isPublic: false);

        var result = await ctx.Community.CastVoteAsync(reportId, ReportTestContext.Member(Voter), ReportVoteTypes.Confirm);

        Assert.False(result.Succeeded);
        Assert.Equal(CommunityStatus.ReportNotFound, result.Status);
    }

    // ------------------------------------------------------------- vote rules

    [Fact]
    public async Task SwitchingAVoteUpdatesTheExistingRowInsteadOfAddingAnother()
    {
        using var ctx = new ReportTestContext();
        var reportId = await AddPendingHazardAsync(ctx);

        await ctx.Community.CastVoteAsync(reportId, ReportTestContext.Member(Voter), ReportVoteTypes.Confirm);
        var result = await ctx.Community.CastVoteAsync(reportId, ReportTestContext.Member(Voter), ReportVoteTypes.Dispute);

        Assert.Equal(1, await ctx.Db.ReportVotes.CountAsync(v => v.ReportId == reportId));
        Assert.Equal(0, result.Data!.ConfirmCount);
        Assert.Equal(1, result.Data.DisputeCount);
    }

    [Fact]
    public async Task RepeatingTheActiveVoteWithdrawsIt()
    {
        using var ctx = new ReportTestContext();
        var reportId = await AddPendingHazardAsync(ctx);

        await ctx.Community.CastVoteAsync(reportId, ReportTestContext.Member(Voter), ReportVoteTypes.Confirm);
        var result = await ctx.Community.CastVoteAsync(reportId, ReportTestContext.Member(Voter), ReportVoteTypes.Confirm);

        Assert.Equal(0, await ctx.Db.ReportVotes.CountAsync(v => v.ReportId == reportId));
        Assert.Null(result.Data!.CurrentUserVote);
    }

    [Fact]
    public async Task ManyVotesFromOneMemberNeverCreateMoreThanOneRow()
    {
        using var ctx = new ReportTestContext();
        var reportId = await AddPendingHazardAsync(ctx);

        await ctx.Community.CastVoteAsync(reportId, ReportTestContext.Member(Voter), ReportVoteTypes.Confirm);
        await ctx.Community.CastVoteAsync(reportId, ReportTestContext.Member(Voter), ReportVoteTypes.Dispute);
        await ctx.Community.CastVoteAsync(reportId, ReportTestContext.Member(Voter), ReportVoteTypes.Confirm);

        Assert.Equal(1, await ctx.Db.ReportVotes.CountAsync(v => v.ReportId == reportId));
    }

    [Fact]
    public async Task AReporterCannotVoteOnTheirOwnReport()
    {
        using var ctx = new ReportTestContext();
        var reportId = await AddPendingHazardAsync(ctx);

        var result = await ctx.Community.CastVoteAsync(reportId, ReportTestContext.Member(Owner), ReportVoteTypes.Confirm);

        Assert.False(result.Succeeded);
        Assert.Equal(CommunityStatus.OwnReport, result.Status);
        Assert.Equal(0, await ctx.Db.ReportVotes.CountAsync());
    }

    [Fact]
    public async Task TheOwnerCanStillOpenTheirOwnPendingReport()
    {
        using var ctx = new ReportTestContext();
        var reportId = await AddPendingHazardAsync(ctx);

        var summary = await ctx.Community.GetVoteSummaryAsync(reportId, ReportTestContext.Member(Owner));

        Assert.NotNull(summary);
        Assert.False(summary!.CanVote);
        Assert.Equal("Community feedback is available from other users.", summary.CannotVoteReason);
    }

    [Fact]
    public async Task StaffDoNotCastCommunityVotes()
    {
        using var ctx = new ReportTestContext();
        var reportId = await AddPendingHazardAsync(ctx);

        // Moderators set the official status, so letting them vote would pollute
        // the very signal they use to decide.
        var result = await ctx.Community.CastVoteAsync(reportId, ReportTestContext.Staff(Moderator), ReportVoteTypes.Confirm);

        Assert.False(result.Succeeded);
        Assert.Equal(CommunityStatus.StaffCannotVote, result.Status);
        Assert.Equal(0, await ctx.Db.ReportVotes.CountAsync());
    }

    [Fact]
    public async Task StaffSeeTheCommunitySignalWithoutBeingAbleToVote()
    {
        using var ctx = new ReportTestContext();
        var reportId = await AddPendingHazardAsync(ctx);
        await ctx.Community.CastVoteAsync(reportId, ReportTestContext.Member(Voter), ReportVoteTypes.Confirm);

        var summary = await ctx.Community.GetVoteSummaryAsync(reportId, ReportTestContext.Staff(Moderator));

        Assert.Equal(1, summary!.ConfirmCount);
        Assert.False(summary.CanVote);
        Assert.Equal("Moderators decide the official status instead of voting.", summary.CannotVoteReason);
    }

    [Fact]
    public async Task AnAnonymousVisitorCannotSeeAPendingReportAtAll()
    {
        using var ctx = new ReportTestContext();
        var reportId = await AddPendingHazardAsync(ctx);

        Assert.Null(await ctx.Community.GetVoteSummaryAsync(reportId, ReportTestContext.Anonymous));
    }

    [Fact]
    public async Task AnAnonymousVisitorSeesAVerifiedReportButCannotVote()
    {
        using var ctx = new ReportTestContext();
        var reportId = await AddPendingHazardAsync(ctx);
        ctx.SetStatus(reportId, ReportStatusCodes.Verified);

        var summary = await ctx.Community.GetVoteSummaryAsync(reportId, ReportTestContext.Anonymous);

        Assert.NotNull(summary);
        Assert.False(summary!.CanVote);
        Assert.Equal("Sign in to add your confirmation.", summary.CannotVoteReason);
    }

    [Theory]
    [InlineData("UPVOTE")]
    [InlineData("")]
    [InlineData("confirm; DROP TABLE report_votes")]
    public async Task AnUnsupportedVoteTypeIsRejected(string voteType)
    {
        using var ctx = new ReportTestContext();
        var reportId = await AddPendingHazardAsync(ctx);

        var result = await ctx.Community.CastVoteAsync(reportId, ReportTestContext.Member(Voter), voteType);

        Assert.False(result.Succeeded);
        Assert.Equal(CommunityStatus.InvalidVoteType, result.Status);
        Assert.Equal(0, await ctx.Db.ReportVotes.CountAsync());
    }

    [Fact]
    public async Task VoteTypeIsAcceptedCaseInsensitivelyButStoredAsTheEnumValue()
    {
        using var ctx = new ReportTestContext();
        var reportId = await AddPendingHazardAsync(ctx);

        await ctx.Community.CastVoteAsync(reportId, ReportTestContext.Member(Voter), "confirm");

        Assert.Equal(ReportVoteTypes.Confirm, (await ctx.Db.ReportVotes.SingleAsync()).VoteType);
    }

    [Fact]
    public async Task VotesNeverChangeTheReportStatus()
    {
        using var ctx = new ReportTestContext();
        var reportId = await AddPendingHazardAsync(ctx);
        var before = ctx.Db.Reports.Single(r => r.ReportId == reportId).StatusId;

        await ctx.Community.CastVoteAsync(reportId, ReportTestContext.Member(Voter), ReportVoteTypes.Confirm);

        Assert.Equal(before, ctx.Db.Reports.Single(r => r.ReportId == reportId).StatusId);
    }

    [Fact]
    public async Task ConsensusStaysNeutralUntilEnoughPeopleRespond()
    {
        using var ctx = new ReportTestContext();
        var reportId = await AddPendingHazardAsync(ctx);

        await ctx.Community.CastVoteAsync(reportId, ReportTestContext.Member(Voter), ReportVoteTypes.Confirm);
        var summary = await ctx.Community.GetVoteSummaryAsync(reportId, ReportTestContext.Member(Voter));

        Assert.Equal("Not enough signal", summary!.ConsensusLabel);
    }
}
