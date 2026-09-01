using System;
using System.Collections.Generic;

namespace SafePathBD.Web.Models.Entities;

/// <summary>
/// User-specific safety, report, route and system notifications.
/// </summary>
public partial class Notifications
{
    public ulong NotificationId { get; set; }

    public ulong UserId { get; set; }

    public ushort NotificationTypeId { get; set; }

    public ulong? ReportId { get; set; }

    public ulong? RouteId { get; set; }

    public ulong? RoadSegmentId { get; set; }

    public string Title { get; set; } = null!;

    public string Message { get; set; } = null!;

    public bool IsRead { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? ReadAt { get; set; }

    public virtual NotificationTypes NotificationType { get; set; } = null!;

    public virtual Reports? Report { get; set; }

    public virtual RoadSegments? RoadSegment { get; set; }

    public virtual Routes? Route { get; set; }

    public virtual Users User { get; set; } = null!;
}
