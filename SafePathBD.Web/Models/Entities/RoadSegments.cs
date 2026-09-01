using System;
using System.Collections.Generic;

namespace SafePathBD.Web.Models.Entities;

/// <summary>
/// Smaller road sections used for risk scoring and route calculation.
/// </summary>
public partial class RoadSegments
{
    public ulong RoadSegmentId { get; set; }

    public ulong RoadId { get; set; }

    public ulong StartLocationId { get; set; }

    public ulong EndLocationId { get; set; }

    public string? SegmentName { get; set; }

    public decimal DistanceKm { get; set; }

    public decimal? AverageTravelTimeMin { get; set; }

    public ushort? SpeedLimitKmh { get; set; }

    public byte? LaneCount { get; set; }

    public bool IsOneWay { get; set; }

    public string? EncodedPolyline { get; set; }

    public bool? IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual ICollection<Accidents> Accidents { get; set; } = new List<Accidents>();

    public virtual Locations EndLocation { get; set; } = null!;

    public virtual ICollection<Notifications> Notifications { get; set; } = new List<Notifications>();

    public virtual ICollection<Reports> Reports { get; set; } = new List<Reports>();

    public virtual Roads Road { get; set; } = null!;

    public virtual ICollection<RoadConditions> RoadConditions { get; set; } = new List<RoadConditions>();

    public virtual ICollection<RouteSegments> RouteSegments { get; set; } = new List<RouteSegments>();

    public virtual ICollection<SafetyScores> SafetyScores { get; set; } = new List<SafetyScores>();

    public virtual Locations StartLocation { get; set; } = null!;

    public virtual ICollection<TrafficConditions> TrafficConditions { get; set; } = new List<TrafficConditions>();

    public virtual ICollection<WeatherConditions> WeatherConditions { get; set; } = new List<WeatherConditions>();
}
