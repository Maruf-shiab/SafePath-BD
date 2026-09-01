using SafePathBD.Web.Models.DTOs.Reports;

namespace SafePathBD.Web.Models.DTOs.Moderation;

/// <summary>One row in the review queue. Aggregates are computed in a single grouped query.</summary>
public sealed record ModerationQueueItemDto(
    ulong ReportId,
    string ReportType,
    string Title,
    string StatusCode,
    string StatusName,
    DateTime ReportedAt,
    string? AreaName,
    string? City,
    string? SeverityName,
    string? AccidentTypeName,
    string? HazardTypeName,
    string? RiskLevel,
    int ImageCount,
    int ConfirmCount,
    int DisputeCount,
    bool IsPublic);

/// <summary>Counts shown above the queue, each one a real database aggregate.</summary>
public sealed record ModerationCountsDto(
    int Pending,
    int UnderReview,
    int NeedsInfo,
    int Verified,
    int Rejected,
    int Duplicate,
    int Resolved)
{
    /// <summary>Everything still awaiting a moderator decision.</summary>
    public int OpenTotal => Pending + UnderReview + NeedsInfo;
}

/// <summary>
/// Everything a reviewer needs on one screen. Includes reporter identity, which is
/// permitted for moderation but must never reach a public projection.
/// </summary>
public sealed record ModerationReportDto(
    ReportDetailsDto Report,
    string? ReporterName,
    string? ReporterEmail,
    ulong? ReporterUserId,
    int ConfirmCount,
    int DisputeCount,
    int CommentCount,
    IReadOnlyList<ReportVerificationEntryDto> History,
    IReadOnlyList<string> AllowedTransitions,
    ulong? PromotedAccidentId);

/// <summary>Result of a moderation decision.</summary>
public enum ModerationStatus
{
    Success,
    NotFound,
    InvalidTransition,
    NoteRequired,
    StaleState,
    StatusConfigurationMissing,
    PromotionFailed
}

public sealed record ModerationResult(
    ModerationStatus Status,
    string? Message = null,
    string? NewStatusCode = null,
    string? NewStatusName = null,
    ulong? CreatedAccidentId = null)
{
    public bool Succeeded => Status == ModerationStatus.Success;

    public static ModerationResult Ok(string statusCode, string statusName, ulong? accidentId = null) =>
        new(ModerationStatus.Success, null, statusCode, statusName, accidentId);

    public static ModerationResult Fail(ModerationStatus status, string message) => new(status, message);
}

/// <summary>A moderation decision request. The reviewer id always comes from the server.</summary>
public sealed record ModerationDecision(
    ulong ReportId,
    ulong ReviewerUserId,
    string TargetStatusCode,
    string? Note,
    string? ExpectedCurrentStatusCode);

/// <summary>Recent privileged activity for the admin dashboard.</summary>
public sealed record AdminActionEntryDto(
    ulong AdminActionId,
    string ActionType,
    string? EntityType,
    ulong? EntityId,
    string? Description,
    string? ActorName,
    DateTime ActionAt);
