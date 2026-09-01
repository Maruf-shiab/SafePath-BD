using System;
using System.Collections.Generic;

namespace SafePathBD.Web.Models.Entities;

/// <summary>
/// Calculated safety score snapshots for road segments. Higher score means safer.
/// </summary>
public partial class SafetyScores
{
    public ulong SafetyScoreId { get; set; }

    public ulong RoadSegmentId { get; set; }

    public decimal OverallSafetyScore { get; set; }

    public string RiskLevel { get; set; } = null!;

    public string MethodologyVersion { get; set; } = null!;

    public DateTime CalculatedAt { get; set; }

    public DateTime? ValidUntil { get; set; }

    public virtual RoadSegments RoadSegment { get; set; } = null!;

    public virtual ICollection<SafetyScoreFactors> SafetyScoreFactors { get; set; } = new List<SafetyScoreFactors>();
}
