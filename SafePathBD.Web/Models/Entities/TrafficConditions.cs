using System;
using System.Collections.Generic;

namespace SafePathBD.Web.Models.Entities;

/// <summary>
/// Traffic snapshots used by fastest and safest route calculations.
/// </summary>
public partial class TrafficConditions
{
    public ulong TrafficConditionId { get; set; }

    public ulong RoadSegmentId { get; set; }

    public string TrafficLevel { get; set; } = null!;

    public decimal? AverageSpeedKmh { get; set; }

    public decimal CongestionScore { get; set; }

    public string? Source { get; set; }

    public DateTime RecordedAt { get; set; }

    public virtual RoadSegments RoadSegment { get; set; } = null!;
}
