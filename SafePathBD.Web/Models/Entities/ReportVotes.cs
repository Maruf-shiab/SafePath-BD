using System;
using System.Collections.Generic;

namespace SafePathBD.Web.Models.Entities;

/// <summary>
/// Community confirmation or dispute votes for reports.
/// </summary>
public partial class ReportVotes
{
    public ulong VoteId { get; set; }

    public ulong ReportId { get; set; }

    public ulong UserId { get; set; }

    public string VoteType { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual Reports Report { get; set; } = null!;

    public virtual Users User { get; set; } = null!;
}
