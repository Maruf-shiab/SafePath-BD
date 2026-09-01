using System;
using System.Collections.Generic;

namespace SafePathBD.Web.Models.Entities;

/// <summary>
/// Reusable map locations used by reports, routes, roads and emergency services.
/// </summary>
public partial class Locations
{
    public ulong LocationId { get; set; }

    public decimal Latitude { get; set; }

    public decimal Longitude { get; set; }

    public string? AddressLine { get; set; }

    public string? LandmarkName { get; set; }

    public string? AreaName { get; set; }

    public string? City { get; set; }

    public string? District { get; set; }

    public string? DivisionName { get; set; }

    public string Country { get; set; } = null!;

    public string PlaceProvider { get; set; } = null!;

    public string? ExternalPlaceId { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual ICollection<Accidents> Accidents { get; set; } = new List<Accidents>();

    public virtual ICollection<EmergencyServices> EmergencyServices { get; set; } = new List<EmergencyServices>();

    public virtual ICollection<Reports> Reports { get; set; } = new List<Reports>();

    public virtual ICollection<RoadSegments> RoadSegmentsEndLocation { get; set; } = new List<RoadSegments>();

    public virtual ICollection<RoadSegments> RoadSegmentsStartLocation { get; set; } = new List<RoadSegments>();

    public virtual ICollection<Routes> RoutesDestinationLocation { get; set; } = new List<Routes>();

    public virtual ICollection<Routes> RoutesStartLocation { get; set; } = new List<Routes>();

    public virtual ICollection<SavedPlaces> SavedPlaces { get; set; } = new List<SavedPlaces>();
}
