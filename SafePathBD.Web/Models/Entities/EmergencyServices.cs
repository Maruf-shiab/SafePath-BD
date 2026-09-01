using System;
using System.Collections.Generic;

namespace SafePathBD.Web.Models.Entities;

/// <summary>
/// Emergency facilities displayed near a user, route or accident location.
/// </summary>
public partial class EmergencyServices
{
    public ulong EmergencyServiceId { get; set; }

    public ushort ServiceTypeId { get; set; }

    public ulong LocationId { get; set; }

    public string ServiceName { get; set; } = null!;

    public string? Phone { get; set; }

    public string? EmergencyPhone { get; set; }

    public string? OpeningHours { get; set; }

    public bool Is24Hours { get; set; }

    public string? WebsiteUrl { get; set; }

    public bool IsVerified { get; set; }

    public bool? IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual Locations Location { get; set; } = null!;

    public virtual EmergencyServiceTypes ServiceType { get; set; } = null!;
}
