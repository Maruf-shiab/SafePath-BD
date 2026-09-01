using System;
using System.Collections.Generic;

namespace SafePathBD.Web.Models.Entities;

/// <summary>
/// Weather snapshots for weather-aware safety scoring.
/// </summary>
public partial class WeatherConditions
{
    public ulong WeatherConditionId { get; set; }

    public ulong RoadSegmentId { get; set; }

    public string WeatherType { get; set; } = null!;

    public decimal? TemperatureC { get; set; }

    public decimal? RainfallMm { get; set; }

    public decimal? VisibilityMeters { get; set; }

    public decimal WeatherRiskScore { get; set; }

    public string? Source { get; set; }

    public DateTime RecordedAt { get; set; }

    public virtual RoadSegments RoadSegment { get; set; } = null!;
}
