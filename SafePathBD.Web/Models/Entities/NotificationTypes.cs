using System;
using System.Collections.Generic;

namespace SafePathBD.Web.Models.Entities;

/// <summary>
/// Categories of notifications sent by the platform.
/// </summary>
public partial class NotificationTypes
{
    public ushort NotificationTypeId { get; set; }

    public string TypeCode { get; set; } = null!;

    public string TypeName { get; set; } = null!;

    public string? Description { get; set; }

    public virtual ICollection<Notifications> Notifications { get; set; } = new List<Notifications>();
}
