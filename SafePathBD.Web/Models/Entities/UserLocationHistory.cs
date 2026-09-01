using System;
using System.Collections.Generic;

namespace SafePathBD.Web.Models.Entities;

/// <summary>
/// Optional, consent-based location captures for user actions; not continuous tracking.
/// </summary>
public partial class UserLocationHistory
{
    public ulong UserLocationId { get; set; }

    public ulong UserId { get; set; }

    public decimal Latitude { get; set; }

    public decimal Longitude { get; set; }

    public decimal? AccuracyMeters { get; set; }

    public string Purpose { get; set; } = null!;

    public DateTime CapturedAt { get; set; }

    public virtual Users User { get; set; } = null!;
}
