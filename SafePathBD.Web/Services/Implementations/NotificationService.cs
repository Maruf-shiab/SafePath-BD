using Microsoft.EntityFrameworkCore;
using SafePathBD.Web.Common;
using SafePathBD.Web.Data;
using SafePathBD.Web.Models.DTOs.Notifications;
using SafePathBD.Web.Models.Entities;
using SafePathBD.Web.Services.Interfaces;

namespace SafePathBD.Web.Services.Implementations;

public sealed class NotificationService : INotificationService
{
    public const int MaxPageSize = 50;
    private readonly SafePathDbContext _db;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(SafePathDbContext db, ILogger<NotificationService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<PagedResult<NotificationItemDto>> GetPageAsync(
        ulong userId,
        string? readFilter,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);
        var filter = NotificationReadFilters.Normalize(readFilter);

        var query = _db.Notifications.AsNoTracking().Where(n => n.UserId == userId);
        query = filter switch
        {
            NotificationReadFilters.Unread => query.Where(n => !n.IsRead),
            NotificationReadFilters.Read => query.Where(n => n.IsRead),
            _ => query
        };

        var total = await query.CountAsync(cancellationToken);
        var rows = await query
            .OrderByDescending(n => n.CreatedAt)
            .ThenByDescending(n => n.NotificationId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(NotificationRowProjection)
            .ToListAsync(cancellationToken);

        return new PagedResult<NotificationItemDto>(rows.Select(ToItem).ToList(), page, pageSize, total);
    }

    public async Task<IReadOnlyList<NotificationItemDto>> GetLatestAsync(
        ulong userId,
        int take,
        CancellationToken cancellationToken = default) =>
        (await _db.Notifications.AsNoTracking()
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAt)
            .ThenByDescending(n => n.NotificationId)
            .Take(Math.Clamp(take, 1, 8))
            .Select(NotificationRowProjection)
            .ToListAsync(cancellationToken))
            .Select(ToItem)
            .ToList();

    public Task<int> GetUnreadCountAsync(ulong userId, CancellationToken cancellationToken = default) =>
        _db.Notifications.AsNoTracking().CountAsync(n => n.UserId == userId && !n.IsRead, cancellationToken);

    public async Task<NotificationItemDto?> GetOwnedAsync(
        ulong userId,
        ulong notificationId,
        CancellationToken cancellationToken = default)
    {
        var row = await _db.Notifications.AsNoTracking()
            .Where(n => n.UserId == userId && n.NotificationId == notificationId)
            .Select(NotificationRowProjection)
            .SingleOrDefaultAsync(cancellationToken);

        return row is null ? null : ToItem(row);
    }

    public async Task<bool> MarkReadAsync(
        ulong userId,
        ulong notificationId,
        CancellationToken cancellationToken = default)
    {
        // Ownership is part of the database predicate; a guessed id from another account is never loaded.
        var notification = await _db.Notifications
            .SingleOrDefaultAsync(n => n.UserId == userId && n.NotificationId == notificationId, cancellationToken);

        if (notification is null)
        {
            return false;
        }

        if (notification.IsRead)
        {
            return true;
        }

        notification.IsRead = true;
        notification.ReadAt = DateTime.Now;
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<int> MarkAllReadAsync(ulong userId, CancellationToken cancellationToken = default)
    {
        var now = DateTime.Now;

        // Production MySQL can update the scoped set without materializing every row.
        // The fallback keeps the in-memory test provider deterministic.
        if (_db.Database.IsRelational())
        {
            return await _db.Notifications
                .Where(n => n.UserId == userId && !n.IsRead)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(n => n.IsRead, true)
                    .SetProperty(n => n.ReadAt, (DateTime?)now), cancellationToken);
        }

        var unread = await _db.Notifications
            .Where(n => n.UserId == userId && !n.IsRead)
            .ToListAsync(cancellationToken);

        foreach (var notification in unread)
        {
            notification.IsRead = true;
            notification.ReadAt = now;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return unread.Count;
    }

    public async Task<bool> QueueReportStatusNotificationAsync(
        ReportStatusNotificationRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.ReporterUserId is null)
        {
            return false;
        }

        var content = BuildReportStatusContent(request.StatusCode, request.ReportTitle, request.ReviewerNote);
        if (content is null)
        {
            return false;
        }

        var typeId = await _db.NotificationTypes
            .Where(t => t.TypeCode == content.TypeCode)
            .Select(t => (ushort?)t.NotificationTypeId)
            .SingleOrDefaultAsync(cancellationToken);

        if (typeId is null)
        {
            // Lookup configuration is a deployment concern. Do not invent/seed rows from application code.
            _logger.LogWarning(
                "Notification type {TypeCode} is not configured; report {ReportId} notification was skipped.",
                content.TypeCode,
                request.ReportId);
            return false;
        }

        _db.Notifications.Add(new Notifications
        {
            UserId = request.ReporterUserId.Value,
            NotificationTypeId = typeId.Value,
            ReportId = request.ReportId,
            Title = content.Title,
            Message = content.Message,
            IsRead = false,
            CreatedAt = request.OccurredAt
        });

        return true;
    }

    private static NotificationContent? BuildReportStatusContent(string statusCode, string reportTitle, string? reviewerNote)
    {
        var safeTitle = string.IsNullOrWhiteSpace(reportTitle) ? "your report" : reportTitle.Trim();
        var note = string.IsNullOrWhiteSpace(reviewerNote) ? null : reviewerNote.Trim();

        return statusCode switch
        {
            ReportStatusCodes.Verified => new(
                NotificationTypeCodes.ReportVerified,
                "Report verified",
                $"Your report \"{safeTitle}\" was verified and is now trusted by SafePath BD."),

            ReportStatusCodes.Rejected => new(
                NotificationTypeCodes.ReportRejected,
                "Report rejected",
                $"Your report \"{safeTitle}\" was rejected after review."),

            ReportStatusCodes.Resolved => new(
                NotificationTypeCodes.ReportResolved,
                "Report resolved",
                $"Your report \"{safeTitle}\" was marked resolved."),

            // The existing lookup table has no NEEDS_INFO or DUPLICATE notification type.
            // Chunk 5 therefore uses the seeded SYSTEM type instead of changing lookup data.
            ReportStatusCodes.NeedsInfo => new(
                NotificationTypeCodes.System,
                "More information needed",
                note is null
                    ? $"A reviewer needs more information for your report \"{safeTitle}\"."
                    : $"A reviewer needs more information for your report \"{safeTitle}\": {note}"),

            ReportStatusCodes.Duplicate => new(
                NotificationTypeCodes.System,
                "Report marked duplicate",
                $"Your report \"{safeTitle}\" was marked as a duplicate of an existing issue."),

            _ => null
        };
    }

    private static string? BuildActionUrl(ulong? reportId) =>
        reportId is null ? null : $"/Reports/Details/{reportId.Value}";

    private static NotificationItemDto ToItem(NotificationRow row) =>
        new(
            row.NotificationId,
            row.TypeCode,
            row.TypeName,
            row.Title,
            row.Message,
            row.IsRead,
            row.CreatedAt,
            row.ReadAt,
            row.ReportId,
            BuildActionUrl(row.ReportId));

    private static System.Linq.Expressions.Expression<Func<Notifications, NotificationRow>> NotificationRowProjection =>
        n => new NotificationRow(
            n.NotificationId,
            n.NotificationType.TypeCode,
            n.NotificationType.TypeName,
            n.Title,
            n.Message,
            n.IsRead,
            n.CreatedAt,
            n.ReadAt,
            n.ReportId);

    private sealed record NotificationRow(
        ulong NotificationId,
        string TypeCode,
        string TypeName,
        string Title,
        string Message,
        bool IsRead,
        DateTime CreatedAt,
        DateTime? ReadAt,
        ulong? ReportId);

    private sealed record NotificationContent(string TypeCode, string Title, string Message);
}
