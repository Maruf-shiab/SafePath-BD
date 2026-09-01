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

    private static async Task<ulong> AddPublicHazardAsync(ReportTestContext ctx)
    {
        var result = await ctx.Hazards.CreateAsync(
            new CreateHazardReportRequest(Owner, "Pothole", null, Point, 1, HazardRiskLevels.High, DateTime.Now, null),
            Array.Empty<StoredImage>());

        ctx.SetStatus(result.ReportId, ReportStatusCodes.Verified);
        return result.ReportId;
    }

    [Fact]
    public async Task ASignedInUserCanConfirmAReport()
    {
        using var ctx = new ReportTestContext();
        var reportId = await AddPublicHazardAsync(ctx);

        var result = await ctx.Community.CastVoteAsync(reportId, Voter, ReportVoteTypes.Confirm);

        Assert.True(result.Succeeded);
        Assert.Equal(1, result.Data!.ConfirmCount);
        Assert.Equal(0, result.Data.DisputeCount);
        Assert.Equal(ReportVoteTypes.Confirm, result.Data.CurrentUserVote);
    }

    [Fact]
    public async Task ASignedInUserCanDisputeAReport()
    {
        using var ctx = new ReportTestContext();
        var reportId = await AddPublicHazardAsync(ctx);

        var result = await ctx.Community.CastVoteAsync(reportId, Voter, ReportVoteTypes.Dispute);

        Assert.True(result.Succeeded);
        Assert.Equal(1, result.Data!.DisputeCount);
        Assert.Equal(ReportVoteTypes.Dispute, result.Data.CurrentUserVote);
    }

    [Fact]
    public async Task SwitchingAVoteUpdatesTheExistingRowInsteadOfAddingAnother()
    {
        using var ctx = new ReportTestContext();
        var reportId = await AddPublicHazardAsync(ctx);

        await ctx.Community.CastVoteAsync(reportId, Voter, ReportVoteTypes.Confirm);
        var result = await ctx.Community.CastVoteAsync(reportId, Voter, ReportVoteTypes.Dispute);

        Assert.Equal(1, await ctx.Db.ReportVotes.CountAsync(v => v.ReportId == reportId));
        Assert.Equal(0, result.Data!.ConfirmCount);
        Assert.Equal(1, result.Data.DisputeCount);
    }

    [Fact]
    public async Task RepeatingTheActiveVoteWithdrawsIt()
    {
        using var ctx = new ReportTestContext();
        var reportId = await AddPublicHazardAsync(ctx);

        await ctx.Community.CastVoteAsync(reportId, Voter, ReportVoteTypes.Confirm);
        var result = await ctx.Community.CastVoteAsync(reportId, Voter, ReportVoteTypes.Confirm);

        Assert.Equal(0, await ctx.Db.ReportVotes.CountAsync(v => v.ReportId == reportId));
        Assert.Null(result.Data!.CurrentUserVote);
    }

    [Fact]
    public async Task ManyVotesFromOneUserNeverCreateMoreThanOneRow()
    {
        using var ctx = new ReportTestContext();
        var reportId = await AddPublicHazardAsync(ctx);

        await ctx.Community.CastVoteAsync(reportId, Voter, ReportVoteTypes.Confirm);
        await ctx.Community.CastVoteAsync(reportId, Voter, ReportVoteTypes.Dispute);
        await ctx.Community.CastVoteAsync(reportId, Voter, ReportVoteTypes.Confirm);

        Assert.Equal(1, await ctx.Db.ReportVotes.CountAsync(v => v.ReportId == reportId));
    }

    [Fact]
    public async Task AReporterCannotVoteOnTheirOwnReport()
    {
        using var ctx = new ReportTestContext();
        var reportId = await AddPublicHazardAsync(ctx);

        var result = await ctx.Community.CastVoteAsync(reportId, Owner, ReportVoteTypes.Confirm);

        Assert.False(result.Succeeded);
        Assert.Equal(CommunityStatus.OwnReport, result.Status);
        Assert.Equal(0, await ctx.Db.ReportVotes.CountAsync());
    }

    [Fact]
    public async Task TheOwnerIsToldWhyTheyCannotVote()
    {
        using var ctx = new ReportTestContext();
        var reportId = await AddPublicHazardAsync(ctx);

        var summary = await ctx.Community.GetVoteSummaryAsync(reportId, new CommunityViewer(Owner, false));

        Assert.False(summary!.CanVote);
        Assert.Equal("Community feedback is available from other users.", summary.CannotVoteReason);
    }

    [Theory]
    [InlineData("UPVOTE")]
    [InlineData("")]
    [InlineData("confirm; DROP TABLE report_votes")]
    public async Task AnUnsupportedVoteTypeIsRejected(string voteType)
    {
        using var ctx = new ReportTestContext();
        var reportId = await AddPublicHazardAsync(ctx);

        var result = await ctx.Community.CastVoteAsync(reportId, Voter, voteType);

        Assert.False(result.Succeeded);
        Assert.Equal(CommunityStatus.InvalidVoteType, result.Status);
        Assert.Equal(0, await ctx.Db.ReportVotes.CountAsync());
    }

    [Fact]
    public async Task VoteTypeIsAcceptedCaseInsensitivelyButStoredAsTheEnumValue()
    {
        using var ctx = new ReportTestContext();
        var reportId = await AddPublicHazardAsync(ctx);

        await ctx.Community.CastVoteAsync(reportId, Voter, "confirm");

        var stored = await ctx.Db.ReportVotes.SingleAsync();
        Assert.Equal(ReportVoteTypes.Confirm, stored.VoteType);
    }

    [Fact]
    public async Task AReportTheVoterCannotSeeCannotBeVotedOn()
    {
        using var ctx = new ReportTestContext();
        var result = await ctx.Hazards.CreateAsync(
            new CreateHazardReportRequest(Owner, "Private pothole", null, Point, 1, HazardRiskLevels.Low, DateTime.Now, null),
            Array.Empty<StoredImage>());

        // Still PENDING, so it is invisible to everyone except the owner and staff.
        var vote = await ctx.Community.CastVoteAsync(result.ReportId, Voter, ReportVoteTypes.Confirm);

        Assert.False(vote.Succeeded);
        Assert.Equal(CommunityStatus.ReportNotFound, vote.Status);
    }

    [Fact]
    public async Task VotesNeverChangeTheReportStatus()
    {
        using var ctx = new ReportTestContext();
        var reportId = await AddPublicHazardAsync(ctx);
        var before = ctx.Db.Reports.Single(r => r.ReportId == reportId).StatusId;

        await ctx.Community.CastVoteAsync(reportId, Voter, ReportVoteTypes.Confirm);
        await ctx.Community.CastVoteAsync(reportId, Moderator, ReportVoteTypes.Confirm);

        var after = ctx.Db.Reports.Single(r => r.ReportId == reportId).StatusId;
        Assert.Equal(before, after);
    }

    [Fact]
    public async Task ConsensusStaysNeutralUntilEnoughPeopleRespond()
    {
        using var ctx = new ReportTestContext();
        var reportId = await AddPublicHazardAsync(ctx);

        await ctx.Community.CastVoteAsync(reportId, Voter, ReportVoteTypes.Confirm);
        var summary = await ctx.Community.GetVoteSummaryAsync(reportId, new CommunityViewer(Voter, false));

        Assert.Equal("Not enough signal", summary!.ConsensusLabel);
    }
}
