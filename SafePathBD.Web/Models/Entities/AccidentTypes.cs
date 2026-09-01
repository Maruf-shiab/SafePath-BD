using System;
using System.Collections.Generic;

namespace SafePathBD.Web.Models.Entities;

/// <summary>
/// Types of road accidents.
/// </summary>
public partial class AccidentTypes
{
    public ushort AccidentTypeId { get; set; }

    public string TypeName { get; set; } = null!;

    public string? Description { get; set; }

    public decimal DefaultRiskWeight { get; set; }

    public bool? IsActive { get; set; }

    public virtual ICollection<AccidentReports> AccidentReports { get; set; } = new List<AccidentReports>();

    public virtual ICollection<Accidents> Accidents { get; set; } = new List<Accidents>();
}
