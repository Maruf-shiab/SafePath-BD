using System;
using System.Collections.Generic;

namespace SafePathBD.Web.Models.Entities;

/// <summary>
/// Accident-specific fields for reports whose report_type is ACCIDENT.
/// </summary>
public partial class AccidentReports
{
    public ulong ReportId { get; set; }

    public ushort AccidentTypeId { get; set; }

    public byte SeverityId { get; set; }

    public DateTime? AccidentOccurredAt { get; set; }

    public ushort? NumberOfVehicles { get; set; }

    public ushort NumberOfInjured { get; set; }

    public ushort NumberOfDeaths { get; set; }

    public string? WeatherNotes { get; set; }

    public virtual AccidentTypes AccidentType { get; set; } = null!;

    public virtual Reports Report { get; set; } = null!;

    public virtual AccidentSeverities Severity { get; set; } = null!;
}
