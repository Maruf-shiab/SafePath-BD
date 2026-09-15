namespace SafePathBD.Web.Models.DTOs.Notifications;

/// <summary>Safe notification projection used by MVC and JSON endpoints.</summary>
public sealed record NotificationItemDto(
    ulong NotificationId,
    string TypeCode,
    string TypeName,
    string Title,
    string Message,
    bool IsRead,
    DateTime CreatedAt,
    DateTime? ReadAt,
    ulong? ReportId,
    string? ActionUrl);

/// <summary>
/// Data needed to create a notification from a successful report workflow transition.
/// User identity and target report are supplied by server-side moderation data only.
/// </summary>
public sealed record ReportStatusNotificationRequest(
    ulong ReportId,
    ulong? ReporterUserId,
    string ReportTitle,
    string StatusCode,
    string? ReviewerNote,
    DateTime OccurredAt);
