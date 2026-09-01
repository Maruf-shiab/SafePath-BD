using System;
using System.Collections.Generic;

namespace SafePathBD.Web.Models.Entities;

/// <summary>
/// Many-to-many bridge between routes and road segments, preserving segment order.
/// </summary>
public partial class RouteSegments
{
    public ulong RouteSegmentId { get; set; }

    public ulong RouteId { get; set; }

    public ulong RoadSegmentId { get; set; }

    public uint SequenceNo { get; set; }

    public decimal DistanceKm { get; set; }

    public decimal? EstimatedDurationMin { get; set; }

    public decimal? SegmentSafetyScore { get; set; }

    public virtual RoadSegments RoadSegment { get; set; } = null!;

    public virtual Routes Route { get; set; } = null!;
}
