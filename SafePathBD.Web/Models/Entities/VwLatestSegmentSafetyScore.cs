using System;
using System.Collections.Generic;

namespace SafePathBD.Web.Models.Entities;

public partial class VwLatestSegmentSafetyScore
{
    public ulong SafetyScoreId { get; set; }

    public ulong RoadSegmentId { get; set; }

    public decimal OverallSafetyScore { get; set; }

    public string RiskLevel { get; set; } = null!;

    public string MethodologyVersion { get; set; } = null!;

    public DateTime CalculatedAt { get; set; }

    public DateTime? ValidUntil { get; set; }
}
