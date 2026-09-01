using System;
using System.Collections.Generic;

namespace SafePathBD.Web.Models.Entities;

/// <summary>
/// Explainable factor-by-factor breakdown behind each road safety score.
/// </summary>
public partial class SafetyScoreFactors
{
    public ulong FactorId { get; set; }

    public ulong SafetyScoreId { get; set; }

    public string FactorType { get; set; } = null!;

    public decimal? RawValue { get; set; }

    public decimal NormalizedRiskScore { get; set; }

    public decimal FactorWeight { get; set; }

    public decimal WeightedRiskScore { get; set; }

    public string? Details { get; set; }

    public virtual SafetyScores SafetyScore { get; set; } = null!;
}
