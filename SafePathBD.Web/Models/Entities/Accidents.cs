using System;
using System.Collections.Generic;

namespace SafePathBD.Web.Models.Entities;

/// <summary>
/// Verified accident history used for hotspot and road-risk calculations.
/// </summary>
public partial class Accidents
{
    public ulong AccidentId { get; set; }

    public ulong? SourceReportId { get; set; }

    public ulong LocationId { get; set; }

    public ulong? RoadSegmentId { get; set; }

    public ushort AccidentTypeId { get; set; }

    public byte SeverityId { get; set; }

    public DateTime AccidentOccurredAt { get; set; }

    public ushort? NumberOfVehicles { get; set; }

    public ushort NumberOfInjured { get; set; }

    public ushort NumberOfDeaths { get; set; }

    public string? WeatherCondition { get; set; }

    public string? Description { get; set; }

    public ulong? VerifiedBy { get; set; }

    public DateTime? VerifiedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual AccidentTypes AccidentType { get; set; } = null!;

    public virtual Locations Location { get; set; } = null!;

    public virtual RoadSegments? RoadSegment { get; set; }

    public virtual AccidentSeverities Severity { get; set; } = null!;

    public virtual Reports? SourceReport { get; set; }

    public virtual Users? VerifiedByNavigation { get; set; }
}
