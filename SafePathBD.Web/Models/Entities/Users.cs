using System;
using System.Collections.Generic;

namespace SafePathBD.Web.Models.Entities;

/// <summary>
/// Registered SafePath BD users.
/// </summary>
public partial class Users
{
    public ulong UserId { get; set; }

    public string FullName { get; set; } = null!;

    public string Email { get; set; } = null!;

    public string? Phone { get; set; }

    public string PasswordHash { get; set; } = null!;

    public string? ProfileImageUrl { get; set; }

    public bool? IsActive { get; set; }

    public bool EmailVerified { get; set; }

    public DateTime? LastLoginAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual ICollection<Accidents> Accidents { get; set; } = new List<Accidents>();

    public virtual ICollection<AdminActions> AdminActions { get; set; } = new List<AdminActions>();

    public virtual ICollection<Feedback> Feedback { get; set; } = new List<Feedback>();

    public virtual ICollection<Notifications> Notifications { get; set; } = new List<Notifications>();

    public virtual ICollection<ReportComments> ReportComments { get; set; } = new List<ReportComments>();

    public virtual ICollection<ReportVerifications> ReportVerifications { get; set; } = new List<ReportVerifications>();

    public virtual ICollection<ReportVotes> ReportVotes { get; set; } = new List<ReportVotes>();

    public virtual ICollection<Reports> Reports { get; set; } = new List<Reports>();

    public virtual ICollection<RoadConditions> RoadConditions { get; set; } = new List<RoadConditions>();

    public virtual ICollection<Routes> Routes { get; set; } = new List<Routes>();

    public virtual ICollection<SavedPlaces> SavedPlaces { get; set; } = new List<SavedPlaces>();

    public virtual ICollection<SavedRoutes> SavedRoutes { get; set; } = new List<SavedRoutes>();

    public virtual ICollection<SystemSettings> SystemSettings { get; set; } = new List<SystemSettings>();

    public virtual ICollection<UserLocationHistory> UserLocationHistory { get; set; } = new List<UserLocationHistory>();

    public virtual ICollection<UserRoles> UserRoles { get; set; } = new List<UserRoles>();
}
