using System;
using System.Collections.Generic;

namespace SafePathBD.Web.Models.Entities;

/// <summary>
/// Audit history of moderator/admin decisions on reports.
/// </summary>
public partial class ReportVerifications
{
    public ulong VerificationId { get; set; }

    public ulong ReportId { get; set; }

    public ulong? AdminUserId { get; set; }

    public ushort StatusId { get; set; }

    public string? AdminComment { get; set; }

    public DateTime VerifiedAt { get; set; }

    public virtual Users? AdminUser { get; set; }

    public virtual Reports Report { get; set; } = null!;

    public virtual ReportStatuses Status { get; set; } = null!;
}
