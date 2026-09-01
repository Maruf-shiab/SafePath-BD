using System;
using System.Collections.Generic;

namespace SafePathBD.Web.Models.Entities;

/// <summary>
/// Lifecycle states for accident and hazard reports.
/// </summary>
public partial class ReportStatuses
{
    public ushort StatusId { get; set; }

    public string StatusCode { get; set; } = null!;

    public string StatusName { get; set; } = null!;

    public string? Description { get; set; }

    public bool IsClosedStatus { get; set; }

    public virtual ICollection<ReportVerifications> ReportVerifications { get; set; } = new List<ReportVerifications>();

    public virtual ICollection<Reports> Reports { get; set; } = new List<Reports>();
}
