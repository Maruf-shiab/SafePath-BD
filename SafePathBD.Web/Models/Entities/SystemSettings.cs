using System;
using System.Collections.Generic;

namespace SafePathBD.Web.Models.Entities;

/// <summary>
/// Configurable application settings including safety-score weights.
/// </summary>
public partial class SystemSettings
{
    public ulong SettingId { get; set; }

    public string SettingKey { get; set; } = null!;

    public string SettingValue { get; set; } = null!;

    public string DataType { get; set; } = null!;

    public string? Description { get; set; }

    public ulong? UpdatedBy { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual Users? UpdatedByNavigation { get; set; }
}
