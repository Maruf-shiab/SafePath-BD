using System;
using System.Collections.Generic;

namespace SafePathBD.Web.Models.Entities;

/// <summary>
/// Many-to-many bridge between users and roles.
/// </summary>
public partial class UserRoles
{
    public ulong UserRoleId { get; set; }

    public ulong UserId { get; set; }

    public ushort RoleId { get; set; }

    public DateTime AssignedAt { get; set; }

    public virtual Roles Role { get; set; } = null!;

    public virtual Users User { get; set; } = null!;
}
