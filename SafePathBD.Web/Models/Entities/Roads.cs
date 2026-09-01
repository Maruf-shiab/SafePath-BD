using System;
using System.Collections.Generic;

namespace SafePathBD.Web.Models.Entities;

/// <summary>
/// Master list of roads managed by the platform.
/// </summary>
public partial class Roads
{
    public ulong RoadId { get; set; }

    public string? RoadCode { get; set; }

    public string RoadName { get; set; } = null!;

    public string? RoadType { get; set; }

    public string? City { get; set; }

    public string? District { get; set; }

    public ushort? DefaultSpeedLimitKmh { get; set; }

    public bool? IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual ICollection<RoadSegments> RoadSegments { get; set; } = new List<RoadSegments>();
}
