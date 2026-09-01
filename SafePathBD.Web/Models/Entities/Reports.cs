using System;
using System.Collections.Generic;

namespace SafePathBD.Web.Models.Entities;

/// <summary>
/// Parent table for all user-submitted accident and hazard reports.
/// </summary>
public partial class Reports
{
    public ulong ReportId { get; set; }

    public string ReportType { get; set; } = null!;

    public ulong? UserId { get; set; }

    public ulong LocationId { get; set; }

    public ulong? RoadSegmentId { get; set; }

    public ushort StatusId { get; set; }

    public string Title { get; set; } = null!;

    public string? Description { get; set; }

    public bool? IsPublic { get; set; }

    public DateTime ReportedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public DateTime? ResolvedAt { get; set; }

    public virtual AccidentReports? AccidentReports { get; set; }

    public virtual Accidents? Accidents { get; set; }

    public virtual HazardReports? HazardReports { get; set; }

    public virtual Locations Location { get; set; } = null!;

    public virtual ICollection<Notifications> Notifications { get; set; } = new List<Notifications>();

    public virtual ICollection<ReportComments> ReportComments { get; set; } = new List<ReportComments>();

    public virtual ICollection<ReportImages> ReportImages { get; set; } = new List<ReportImages>();

    public virtual ICollection<ReportVerifications> ReportVerifications { get; set; } = new List<ReportVerifications>();

    public virtual ICollection<ReportVotes> ReportVotes { get; set; } = new List<ReportVotes>();

    public virtual RoadSegments? RoadSegment { get; set; }

    public virtual ReportStatuses Status { get; set; } = null!;

    public virtual Users? User { get; set; }
}
