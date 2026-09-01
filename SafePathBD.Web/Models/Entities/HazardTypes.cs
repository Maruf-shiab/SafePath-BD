using System;
using System.Collections.Generic;

namespace SafePathBD.Web.Models.Entities;

/// <summary>
/// Road hazard categories such as pothole, waterlogging or broken signal.
/// </summary>
public partial class HazardTypes
{
    public ushort HazardTypeId { get; set; }

    public string HazardName { get; set; } = null!;

    public string? Description { get; set; }

    public decimal DefaultRiskWeight { get; set; }

    public bool? IsActive { get; set; }

    public virtual ICollection<HazardReports> HazardReports { get; set; } = new List<HazardReports>();
}
