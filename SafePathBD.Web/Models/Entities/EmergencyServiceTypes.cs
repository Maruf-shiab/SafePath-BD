using System;
using System.Collections.Generic;

namespace SafePathBD.Web.Models.Entities;

/// <summary>
/// Hospital, Police Station, Fire Service, Ambulance and similar types.
/// </summary>
public partial class EmergencyServiceTypes
{
    public ushort ServiceTypeId { get; set; }

    public string ServiceTypeName { get; set; } = null!;

    public string? Description { get; set; }

    public virtual ICollection<EmergencyServices> EmergencyServices { get; set; } = new List<EmergencyServices>();
}
