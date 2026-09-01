using System;
using System.Collections.Generic;

namespace SafePathBD.Web.Models.Entities;

/// <summary>
/// Generated route alternatives such as safest, fastest and shortest.
/// </summary>
public partial class Routes
{
    public ulong RouteId { get; set; }

    public ulong? UserId { get; set; }

    public ulong StartLocationId { get; set; }

    public ulong DestinationLocationId { get; set; }

    public string RouteType { get; set; } = null!;

    public decimal TotalDistanceKm { get; set; }

    public decimal? EstimatedDurationMin { get; set; }

    public decimal? OverallSafetyScore { get; set; }

    public string? EncodedPolyline { get; set; }

    public DateTime GeneratedAt { get; set; }

    public DateTime? ExpiresAt { get; set; }

    public virtual Locations DestinationLocation { get; set; } = null!;

    public virtual ICollection<Notifications> Notifications { get; set; } = new List<Notifications>();

    public virtual ICollection<RouteSegments> RouteSegments { get; set; } = new List<RouteSegments>();

    public virtual ICollection<SavedRoutes> SavedRoutes { get; set; } = new List<SavedRoutes>();

    public virtual Locations StartLocation { get; set; } = null!;

    public virtual Users? User { get; set; }
}
