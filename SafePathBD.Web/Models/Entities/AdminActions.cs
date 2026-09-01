using System;
using System.Collections.Generic;

namespace SafePathBD.Web.Models.Entities;

/// <summary>
/// Audit log for important administrator and moderator actions.
/// </summary>
public partial class AdminActions
{
    public ulong AdminActionId { get; set; }

    public ulong? AdminUserId { get; set; }

    public string ActionType { get; set; } = null!;

    public string? EntityType { get; set; }

    public ulong? EntityId { get; set; }

    public string? Description { get; set; }

    public string? MetadataJson { get; set; }

    public DateTime ActionAt { get; set; }

    public virtual Users? AdminUser { get; set; }
}
