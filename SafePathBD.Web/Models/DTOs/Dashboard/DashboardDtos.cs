using SafePathBD.Web.Models.DTOs.Moderation;
using SafePathBD.Web.Models.DTOs.Notifications;
using SafePathBD.Web.Models.DTOs.Reports;

namespace SafePathBD.Web.Models.DTOs.Dashboard;

public sealed record NeedsInfoReportDto(
    ulong ReportId,
    string Title,
    DateTime UpdatedAt,
    string? ReviewerNote);

public sealed record UserActivityItemDto(
    string Kind,
    string Title,
    string Description,
    DateTime OccurredAt,
    ulong? ReportId,
    string? StatusCode);

public sealed record UserDashboardDto(
    int TotalReports,
    int Pending,
    int UnderReview,
    int NeedsInfo,
    int Verified,
    int Rejected,
    int Duplicate,
    int Resolved,
    int UnreadNotifications,
    IReadOnlyList<ReportSummaryDto> RecentReports,
    IReadOnlyList<NeedsInfoReportDto> ActionRequired,
    IReadOnlyList<NotificationItemDto> RecentNotifications,
    IReadOnlyList<UserActivityItemDto> RecentActivity);

public sealed record ModeratorActivityDto(
    ulong VerificationId,
    ulong ReportId,
    string ReportTitle,
    string StatusCode,
    string StatusName,
    string? ReviewerName,
    DateTime OccurredAt);

public sealed record ModeratorDashboardDto(
    ModerationCountsDto Counts,
    IReadOnlyList<ModerationQueueItemDto> QueuePreview,
    IReadOnlyList<ModeratorActivityDto> RecentActivity);

public sealed record AdminDashboardDto(
    int TotalUsers,
    int ActiveUsers,
    int AccidentReports,
    int HazardReports,
    ModerationCountsDto ReportCounts,
    IReadOnlyList<AdminActionEntryDto> RecentActions,
    IReadOnlyList<ModerationQueueItemDto> QueuePreview);
