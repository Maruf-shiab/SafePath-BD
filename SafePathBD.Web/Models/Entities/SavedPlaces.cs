using System;
using System.Collections.Generic;

namespace SafePathBD.Web.Models.Entities;

/// <summary>
/// Named locations saved by users, such as Home or University.
/// </summary>
public partial class SavedPlaces
{
    public ulong SavedPlaceId { get; set; }

    public ulong UserId { get; set; }

    public ulong LocationId { get; set; }

    public string PlaceName { get; set; } = null!;

    public string PlaceType { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public virtual Locations Location { get; set; } = null!;

    public virtual Users User { get; set; } = null!;
}
