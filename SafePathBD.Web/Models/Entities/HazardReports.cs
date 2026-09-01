using System;
using System.Collections.Generic;

namespace SafePathBD.Web.Models.Entities;

/// <summary>
/// Hazard-specific fields for reports whose report_type is HAZARD.
/// </summary>
public partial class HazardReports
{
    public ulong ReportId { get; set; }

    public ushort HazardTypeId { get; set; }

    public string RiskLevel { get; set; } = null!;

    public DateTime? ObservedAt { get; set; }

    public DateTime? ExpectedClearanceAt { get; set; }

    public virtual HazardTypes HazardType { get; set; } = null!;

    public virtual Reports Report { get; set; } = null!;
}
