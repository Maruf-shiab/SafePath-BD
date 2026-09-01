using System;
using System.Collections.Generic;

namespace SafePathBD.Web.Models.Entities;

/// <summary>
/// Images attached to any accident or hazard report.
/// </summary>
public partial class ReportImages
{
    public ulong ImageId { get; set; }

    public ulong ReportId { get; set; }

    public string ImageUrl { get; set; } = null!;

    public string? Caption { get; set; }

    public DateTime UploadedAt { get; set; }

    public virtual Reports Report { get; set; } = null!;
}
