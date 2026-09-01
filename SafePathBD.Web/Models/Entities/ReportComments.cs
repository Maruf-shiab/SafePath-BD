using System;
using System.Collections.Generic;

namespace SafePathBD.Web.Models.Entities;

/// <summary>
/// Discussion and clarification comments on reports.
/// </summary>
public partial class ReportComments
{
    public ulong CommentId { get; set; }

    public ulong ReportId { get; set; }

    public ulong? UserId { get; set; }

    public ulong? ParentCommentId { get; set; }

    public string CommentText { get; set; } = null!;

    public bool IsDeleted { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual ICollection<ReportComments> InverseParentComment { get; set; } = new List<ReportComments>();

    public virtual ReportComments? ParentComment { get; set; }

    public virtual Reports Report { get; set; } = null!;

    public virtual Users? User { get; set; }
}
