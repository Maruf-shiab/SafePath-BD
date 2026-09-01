using System;
using System.Collections.Generic;

namespace SafePathBD.Web.Models.Entities;

/// <summary>
/// User feedback, suggestions and complaints.
/// </summary>
public partial class Feedback
{
    public ulong FeedbackId { get; set; }

    public ulong? UserId { get; set; }

    public string Subject { get; set; } = null!;

    public string Message { get; set; } = null!;

    public byte? Rating { get; set; }

    public string Status { get; set; } = null!;

    public DateTime SubmittedAt { get; set; }

    public DateTime? ResolvedAt { get; set; }

    public virtual Users? User { get; set; }
}
