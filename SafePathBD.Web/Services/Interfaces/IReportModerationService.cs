using SafePathBD.Web.Common;
using SafePathBD.Web.Models.DTOs.Moderation;

namespace SafePathBD.Web.Services.Interfaces;

public sealed record ModerationQueueQuery(
    string? StatusCode = null,
    string? ReportType = null,
    string? Search = null,
    string? Severity = null,
    string? RiskLevel = null,
    DateTime? From = null,
    DateTime? To = null,
    int Page = 1,
    int PageSize = 20);

public interface IReportModerationService
{
    Task<PagedResult<ModerationQueueItemDto>> GetQueueAsync(ModerationQueueQuery query, CancellationToken cancellationToken = default);

    Task<ModerationCountsDto> GetCountsAsync(CancellationToken cancellationToken = default);

    /// <summary>Full review projection, including reporter identity. Staff-only by construction.</summary>
    Task<ModerationReportDto?> GetForReviewAsync(ulong reportId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Applies a status change atomically: verification history, report status, audit entry
    /// and — for a verified accident — promotion into the trusted <c>accidents</c> table.
    /// </summary>
    Task<ModerationResult> ApplyDecisionAsync(ModerationDecision decision, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AdminActionEntryDto>> GetRecentActionsAsync(int take, CancellationToken cancellationToken = default);
}
