namespace SafePathBD.Web.Integrations.Routing;

public sealed class OsrmRoutingOptions
{
    public const string SectionName = "Routing:OSRM";

    public string BaseUrl { get; set; } = "https://router.project-osrm.org/";
    public string Profile { get; set; } = "driving";
    public int TimeoutSeconds { get; set; } = 12;
    public int CacheMinutes { get; set; } = 20;
    public int IncidentRecheckSeconds { get; set; } = 60;
    public double IncidentProximityMeters { get; set; } = 50;
    public bool DetourFallbackEnabled { get; set; } = true;
    public double DetourOffsetMeters { get; set; } = 600;
    public int MaxDetourAttempts { get; set; } = 4;
    public double MaxDetourDistanceFactor { get; set; } = 1.75;
    public double CautionAlternativeDistanceFactor { get; set; } = 1.25;
    public bool TrafficDetourEnabled { get; set; } = true;
    public double TrafficDetourCongestionThreshold { get; set; } = 55;
    public double TrafficDetourMinCongestionGain { get; set; } = 10;
    public double TrafficDetourMinTimeGainPercent { get; set; } = 0.03;
}
