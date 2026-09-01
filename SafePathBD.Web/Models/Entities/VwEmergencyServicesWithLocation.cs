using System;
using System.Collections.Generic;

namespace SafePathBD.Web.Models.Entities;

public partial class VwEmergencyServicesWithLocation
{
    public ulong EmergencyServiceId { get; set; }

    public string ServiceTypeName { get; set; } = null!;

    public string ServiceName { get; set; } = null!;

    public string? Phone { get; set; }

    public string? EmergencyPhone { get; set; }

    public string? OpeningHours { get; set; }

    public bool Is24Hours { get; set; }

    public string? WebsiteUrl { get; set; }

    public bool IsVerified { get; set; }

    public bool? IsActive { get; set; }

    public decimal Latitude { get; set; }

    public decimal Longitude { get; set; }

    public string? AddressLine { get; set; }

    public string? LandmarkName { get; set; }

    public string? AreaName { get; set; }

    public string? City { get; set; }

    public string? District { get; set; }
}
