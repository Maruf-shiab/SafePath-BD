namespace SafePathBD.Web.Common;

/// <summary>
/// The single source of truth for who may see and act on a report.
/// Every service and controller must go through here so the rules cannot drift apart.
/// </summary>
public static class ReportVisibility
{
    /// <summary>
    /// Public reports in these states still need community input, so signed-in members
    /// may open them to confirm, dispute or add context. They are never public knowledge.
    /// </summary>
    public static readonly IReadOnlyList<string> CommunityReviewStatuses = new[]
    {
        ReportStatusCodes.Pending,
        ReportStatusCodes.UnderReview,
        ReportStatusCodes.NeedsInfo
    };

    /// <summary>What an anonymous visitor may see: verified and public, nothing else.</summary>
    public static bool IsPubliclyVisible(string statusCode, bool isPublic) =>
        isPublic && statusCode == ReportStatusCodes.Verified;

    /// <summary>A public report still awaiting an official decision.</summary>
    public static bool IsCommunityReviewable(string statusCode, bool isPublic) =>
        isPublic && CommunityReviewStatuses.Contains(statusCode);

    /// <summary>
    /// Whether the viewer may open the report at all. Signed-in members additionally
    /// get access to community-reviewable reports that anonymous visitors cannot see.
    /// </summary>
    public static bool CanView(string statusCode, bool isPublic, bool isOwner, bool isStaff, bool isAuthenticated) =>
        isOwner
        || isStaff
        || IsPubliclyVisible(statusCode, isPublic)
        || (isAuthenticated && IsCommunityReviewable(statusCode, isPublic));

    /// <summary>
    /// Community voting is for ordinary members. Staff are deliberately excluded: they
    /// already decide the official status, so letting them vote would contaminate the
    /// very signal they use to make that decision.
    /// </summary>
    public static bool CanVote(string statusCode, bool isPublic, bool isOwner, bool isStaff, bool isAuthenticated) =>
        isAuthenticated
        && !isOwner
        && !isStaff
        && CanView(statusCode, isPublic, isOwner, isStaff, isAuthenticated);

    /// <summary>Anyone who can see a report and is signed in may discuss it.</summary>
    public static bool CanComment(string statusCode, bool isPublic, bool isOwner, bool isStaff, bool isAuthenticated) =>
        isAuthenticated && CanView(statusCode, isPublic, isOwner, isStaff, isAuthenticated);

    /// <summary>Explains a disabled vote control without leaking anything.</summary>
    public static string? VoteBlockedReason(bool isOwner, bool isStaff, bool isAuthenticated) =>
        !isAuthenticated ? "Sign in to add your confirmation."
        : isOwner ? "Community feedback is available from other users."
        : isStaff ? "Moderators decide the official status instead of voting."
        : null;

    /// <summary>True when the report has not yet been officially verified.</summary>
    public static bool IsAwaitingVerification(string statusCode) =>
        statusCode is not (ReportStatusCodes.Verified or ReportStatusCodes.Resolved);
}
