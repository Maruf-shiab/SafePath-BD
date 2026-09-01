using System;
using System.Collections.Generic;

namespace SafePathBD.Web.Models.Entities;

public partial class VwReportOverview
{
    public ulong? ReportId { get; set; }

    public string? ReportType { get; set; }

    public string? Title { get; set; }

    public string? Description { get; set; }

    public string StatusCode { get; set; } = null!;

    public string StatusName { get; set; } = null!;

    public ulong? UserId { get; set; }

    public string? ReporterName { get; set; }

    public ulong? LocationId { get; set; }

    public decimal Latitude { get; set; }

    public decimal Longitude { get; set; }

    public string? LandmarkName { get; set; }

    public string? AreaName { get; set; }

    public ulong? RoadSegmentId { get; set; }

    public DateTime? ReportedAt { get; set; }

    public DateTime? ResolvedAt { get; set; }

    public long? ConfirmVotes { get; set; }

    public long? DisputeVotes { get; set; }
}
