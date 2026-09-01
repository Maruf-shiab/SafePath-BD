namespace SafePathBD.Web.Models.DTOs.Reports;

/// <summary>
/// Aggregate community signal for a report. Voter identities are never included.
/// </summary>
public sealed record ReportVoteSummaryDto(
    ulong ReportId,
    int ConfirmCount,
    int DisputeCount,
    string? CurrentUserVote,
    bool CanVote,
    string? CannotVoteReason)
{
    public int TotalVotes => ConfirmCount + DisputeCount;

    /// <summary>
    /// A deliberately conservative label. Community agreement is not verification, so this
    /// stays "Not enough signal" until a meaningful number of people have responded.
    /// </summary>
    public string ConsensusLabel =>
        TotalVotes < 3 ? "Not enough signal"
        : ConfirmCount >= DisputeCount * 3 ? "Strong agreement"
        : ConfirmCount > DisputeCount ? "Leaning confirmed"
        : DisputeCount > ConfirmCount ? "Contested"
        : "Split";
}

/// <summary>One comment. <paramref name="AuthorName"/> already respects reporter privacy rules.</summary>
public sealed record ReportCommentDto(
    ulong CommentId,
    ulong? ParentCommentId,
    string AuthorName,
    string AuthorInitials,
    bool AuthorIsStaff,
    bool IsOwnComment,
    bool IsDeleted,
    string Text,
    DateTime CreatedAt,
    IReadOnlyList<ReportCommentDto> Replies);

/// <summary>A single recorded moderation decision, oldest first when listed.</summary>
public sealed record ReportVerificationEntryDto(
    ulong VerificationId,
    string StatusCode,
    string StatusName,
    string? ReviewerName,
    string? Note,
    DateTime VerifiedAt);
