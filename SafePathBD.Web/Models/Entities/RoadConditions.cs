using System;
using System.Collections.Generic;

namespace SafePathBD.Web.Models.Entities;

/// <summary>
/// Historical road quality observations for each segment.
/// </summary>
public partial class RoadConditions
{
    public ulong RoadConditionId { get; set; }

    public ulong RoadSegmentId { get; set; }

    public string SurfaceCondition { get; set; } = null!;

    public decimal SurfaceScore { get; set; }

    public decimal? LightingScore { get; set; }

    public decimal? DrainageScore { get; set; }

    public decimal? VisibilityScore { get; set; }

    public decimal OverallConditionScore { get; set; }

    public string? Description { get; set; }

    public string SourceType { get; set; } = null!;

    public ulong? RecordedBy { get; set; }

    public DateTime RecordedAt { get; set; }

    public virtual Users? RecordedByNavigation { get; set; }

    public virtual RoadSegments RoadSegment { get; set; } = null!;
}
