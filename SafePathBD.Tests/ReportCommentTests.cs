using Microsoft.EntityFrameworkCore;
using SafePathBD.Web.Common;
using SafePathBD.Web.Models.DTOs.Reports;
using SafePathBD.Web.Services.Implementations;
using SafePathBD.Web.Services.Interfaces;

namespace SafePathBD.Tests;

/// <summary>Report discussion: permissions, validation and encoding safety.</summary>
public class ReportCommentTests
{
    private const ulong Owner = 7;
    private const ulong Other = 8;
    private const ulong Moderator = 9;

    private static readonly ReportLocationInput Point =
        new(23.7465, 90.3760, "Dhanmondi 27", null, "Dhanmondi", "Dhaka", "Dhaka", "OSM");

    private static async Task<ulong> AddHazardAsync(ReportTestContext ctx, bool verified)
    {
        var result = await ctx.Hazards.CreateAsync(
            new CreateHazardReportRequest(Owner, "Pothole", null, Point, 1, HazardRiskLevels.High, DateTime.Now, null),
            Array.Empty<StoredImage>());

        if (verified)
        {
            ctx.SetStatus(result.ReportId, ReportStatusCodes.Verified);
        }

        return result.ReportId;
    }

    [Fact]
    public async Task AnyoneWhoCanSeeTheReportCanComment()
    {
        using var ctx = new ReportTestContext();
        var reportId = await AddHazardAsync(ctx, verified: true);

        var result = await ctx.Community.AddCommentAsync(reportId, Other, "The hole is deeper on the left lane.", null);

        Assert.True(result.Succeeded);
        Assert.Equal(1, await ctx.Db.ReportComments.CountAsync());
        Assert.Equal("Reporter Two", result.Data!.AuthorName);
    }

    [Fact]
    public async Task CommentsAreLinkedToTheRightReportAndUser()
    {
        using var ctx = new ReportTestContext();
        var reportId = await AddHazardAsync(ctx, verified: true);

        await ctx.Community.AddCommentAsync(reportId, Other, "Still there today.", null);

        var stored = await ctx.Db.ReportComments.SingleAsync();
        Assert.Equal(reportId, stored.ReportId);
        Assert.Equal(Other, stored.UserId);
        Assert.False(stored.IsDeleted);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\n\t  \n")]
    public async Task ABlankCommentIsRejected(string text)
    {
        using var ctx = new ReportTestContext();
        var reportId = await AddHazardAsync(ctx, verified: true);

        var result = await ctx.Community.AddCommentAsync(reportId, Other, text, null);

        Assert.False(result.Succeeded);
        Assert.Equal(CommunityStatus.EmptyComment, result.Status);
        Assert.Equal(0, await ctx.Db.ReportComments.CountAsync());
    }

    [Fact]
    public async Task AnOversizedCommentIsRejected()
    {
        using var ctx = new ReportTestContext();
        var reportId = await AddHazardAsync(ctx, verified: true);

        var tooLong = new string('a', ReportCommunityService.MaxCommentLength + 1);
        var result = await ctx.Community.AddCommentAsync(reportId, Other, tooLong, null);

        Assert.False(result.Succeeded);
        Assert.Equal(CommunityStatus.CommentTooLong, result.Status);
    }

    [Fact]
    public async Task CommentTextIsStoredVerbatimAndNeverInterpreted()
    {
        using var ctx = new ReportTestContext();
        var reportId = await AddHazardAsync(ctx, verified: true);

        const string payload = "<script>alert('xss')</script>";
        var result = await ctx.Community.AddCommentAsync(reportId, Other, payload, null);

        // It stays data. Razor and the DOM-based JS renderer are what stop it becoming markup.
        Assert.Equal(payload, result.Data!.Text);
        Assert.Equal(payload, (await ctx.Db.ReportComments.SingleAsync()).CommentText);
    }

    [Fact]
    public async Task CommentsAreTrimmed()
    {
        using var ctx = new ReportTestContext();
        var reportId = await AddHazardAsync(ctx, verified: true);

        var result = await ctx.Community.AddCommentAsync(reportId, Other, "   spaced out   ", null);

        Assert.Equal("spaced out", result.Data!.Text);
    }

    [Fact]
    public async Task AHiddenReportCannotBeCommentedOn()
    {
        using var ctx = new ReportTestContext();
        var reportId = await AddHazardAsync(ctx, verified: false);

        var result = await ctx.Community.AddCommentAsync(reportId, Other, "Let me in.", null);

        Assert.False(result.Succeeded);
        Assert.Equal(CommunityStatus.ReportNotFound, result.Status);
    }

    [Fact]
    public async Task TheOwnerCanCommentOnTheirOwnPendingReport()
    {
        using var ctx = new ReportTestContext();
        var reportId = await AddHazardAsync(ctx, verified: false);

        var result = await ctx.Community.AddCommentAsync(reportId, Owner, "Adding the detail you asked for.", null);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task AReplyIsAttachedToItsParent()
    {
        using var ctx = new ReportTestContext();
        var reportId = await AddHazardAsync(ctx, verified: true);

        var parent = await ctx.Community.AddCommentAsync(reportId, Other, "Is it fixed?", null);
        var reply = await ctx.Community.AddCommentAsync(reportId, Owner, "Not yet.", parent.Data!.CommentId);

        Assert.True(reply.Succeeded);
        Assert.Equal(parent.Data.CommentId, reply.Data!.ParentCommentId);
    }

    [Fact]
    public async Task RepliesCannotNestMoreThanOneLevel()
    {
        using var ctx = new ReportTestContext();
        var reportId = await AddHazardAsync(ctx, verified: true);

        var parent = await ctx.Community.AddCommentAsync(reportId, Other, "Is it fixed?", null);
        var reply = await ctx.Community.AddCommentAsync(reportId, Owner, "Not yet.", parent.Data!.CommentId);
        var nested = await ctx.Community.AddCommentAsync(reportId, Other, "Thanks.", reply.Data!.CommentId);

        Assert.False(nested.Succeeded);
        Assert.Equal(CommunityStatus.ParentNotFound, nested.Status);
    }

    [Fact]
    public async Task AReplyCannotPointAtACommentOnAnotherReport()
    {
        using var ctx = new ReportTestContext();
        var first = await AddHazardAsync(ctx, verified: true);
        var second = await AddHazardAsync(ctx, verified: true);

        var parent = await ctx.Community.AddCommentAsync(first, Other, "On report one.", null);
        var crossed = await ctx.Community.AddCommentAsync(second, Other, "Wrong thread.", parent.Data!.CommentId);

        Assert.False(crossed.Succeeded);
        Assert.Equal(CommunityStatus.ParentNotFound, crossed.Status);
    }

    [Fact]
    public async Task CommentsAreReturnedWithTheirRepliesAndArePaged()
    {
        using var ctx = new ReportTestContext();
        var reportId = await AddHazardAsync(ctx, verified: true);

        for (var i = 0; i < 12; i++)
        {
            await ctx.Community.AddCommentAsync(reportId, Other, "Comment " + i, null);
        }

        var root = await ctx.Community.AddCommentAsync(reportId, Other, "Newest root", null);
        await ctx.Community.AddCommentAsync(reportId, Owner, "A reply", root.Data!.CommentId);

        var page = await ctx.Community.GetCommentsAsync(reportId, new CommunityViewer(Other, false), 1, 10);

        Assert.Equal(13, page.TotalCount);
        Assert.Equal(10, page.Items.Count);
        Assert.True(page.HasNext);
        Assert.Single(page.Items[0].Replies);
    }

    [Fact]
    public async Task AHiddenReportReturnsNoComments()
    {
        using var ctx = new ReportTestContext();
        var reportId = await AddHazardAsync(ctx, verified: false);
        await ctx.Community.AddCommentAsync(reportId, Owner, "Owner note", null);

        var page = await ctx.Community.GetCommentsAsync(reportId, new CommunityViewer(Other, false), 1, 10);

        Assert.Empty(page.Items);
        Assert.Equal(0, page.TotalCount);
    }

    [Fact]
    public async Task AnAuthorCanRemoveTheirOwnComment()
    {
        using var ctx = new ReportTestContext();
        var reportId = await AddHazardAsync(ctx, verified: true);
        var comment = await ctx.Community.AddCommentAsync(reportId, Other, "Oops.", null);

        var result = await ctx.Community.DeleteCommentAsync(comment.Data!.CommentId, new CommunityViewer(Other, false));

        Assert.True(result.Succeeded);
        Assert.True((await ctx.Db.ReportComments.SingleAsync()).IsDeleted);
    }

    [Fact]
    public async Task AStrangerCannotRemoveSomeoneElsesComment()
    {
        using var ctx = new ReportTestContext();
        var reportId = await AddHazardAsync(ctx, verified: true);
        var comment = await ctx.Community.AddCommentAsync(reportId, Other, "Mine.", null);

        var result = await ctx.Community.DeleteCommentAsync(comment.Data!.CommentId, new CommunityViewer(Owner, false));

        Assert.False(result.Succeeded);
        Assert.False((await ctx.Db.ReportComments.SingleAsync()).IsDeleted);
    }

    [Fact]
    public async Task AModeratorCanRemoveAnyComment()
    {
        using var ctx = new ReportTestContext();
        var reportId = await AddHazardAsync(ctx, verified: true);
        var comment = await ctx.Community.AddCommentAsync(reportId, Other, "Inappropriate.", null);

        var result = await ctx.Community.DeleteCommentAsync(comment.Data!.CommentId, new CommunityViewer(Moderator, true));

        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task ARemovedCommentDoesNotExposeItsOriginalText()
    {
        using var ctx = new ReportTestContext();
        var reportId = await AddHazardAsync(ctx, verified: true);
        var comment = await ctx.Community.AddCommentAsync(reportId, Other, "Sensitive detail", null);
        await ctx.Community.DeleteCommentAsync(comment.Data!.CommentId, new CommunityViewer(Other, false));

        var page = await ctx.Community.GetCommentsAsync(reportId, new CommunityViewer(Other, false), 1, 10);

        Assert.True(page.Items[0].IsDeleted);
        Assert.DoesNotContain("Sensitive", page.Items[0].Text);
        Assert.Equal("Removed comment", page.Items[0].AuthorName);
    }

    [Fact]
    public async Task AModeratorCommentIsBadged()
    {
        using var ctx = new ReportTestContext();
        var reportId = await AddHazardAsync(ctx, verified: true);

        var result = await ctx.Community.AddCommentAsync(reportId, Moderator, "Reviewed on site.", null);

        Assert.True(result.Data!.AuthorIsStaff);
    }
}
