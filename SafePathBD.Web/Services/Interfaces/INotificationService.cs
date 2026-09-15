using SafePathBD.Web.Common;
using SafePathBD.Web.Models.DTOs.Notifications;

namespace SafePathBD.Web.Services.Interfaces;

public interface INotificationService
{
    Task<PagedResult<NotificationItemDto>> GetPageAsync(
        ulong userId,
        string? readFilter,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<NotificationItemDto>> GetLatestAsync(
        ulong userId,
        int take,
        CancellationToken cancellationToken = default);

    Task<int> GetUnreadCountAsync(ulong userId, CancellationToken cancellationToken = default);

    Task<NotificationItemDto?> GetOwnedAsync(
        ulong userId,
        ulong notificationId,
        CancellationToken cancellationToken = default);

    Task<bool> MarkReadAsync(
        ulong userId,
        ulong notificationId,
        CancellationToken cancellationToken = default);

    Task<int> MarkAllReadAsync(ulong userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds one reporter notification to the current DbContext unit of work. The caller owns
    /// SaveChanges/transaction boundaries so moderation state and its notification commit together.
    /// Returns false only when no notification is appropriate/configured.
    /// </summary>
    Task<bool> QueueReportStatusNotificationAsync(
        ReportStatusNotificationRequest request,
        CancellationToken cancellationToken = default);
}
