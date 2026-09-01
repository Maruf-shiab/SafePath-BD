using System;
using System.Collections.Generic;

namespace SafePathBD.Web.Models.Entities;

/// <summary>
/// Routes explicitly bookmarked by users.
/// </summary>
public partial class SavedRoutes
{
    public ulong SavedRouteId { get; set; }

    public ulong UserId { get; set; }

    public ulong RouteId { get; set; }

    public string CustomName { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public virtual Routes Route { get; set; } = null!;

    public virtual Users User { get; set; } = null!;
}
