using System;
using System.Collections.Generic;

namespace SafePathBD.Web.Models.Entities;

/// <summary>
/// Severity levels used to weight accident risk.
/// </summary>
public partial class AccidentSeverities
{
    public byte SeverityId { get; set; }

    public string SeverityName { get; set; } = null!;

    public decimal RiskWeight { get; set; }

    public string? Description { get; set; }

    public virtual ICollection<AccidentReports> AccidentReports { get; set; } = new List<AccidentReports>();

    public virtual ICollection<Accidents> Accidents { get; set; } = new List<Accidents>();
}
