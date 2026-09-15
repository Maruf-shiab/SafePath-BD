namespace SafePathBD.Web.Common;

/// <summary>
/// Stable notification type codes exactly as seeded in notification_types.
/// Chunk 5 deliberately does not insert or alter lookup rows.
/// </summary>
public static class NotificationTypeCodes
{
    public const string HazardAlert = "HAZARD_ALERT";
    public const string AccidentAlert = "ACCIDENT_ALERT";
    public const string EmergencyAlert = "EMERGENCY_ALERT";
    public const string ReportVerified = "REPORT_VERIFIED";
    public const string ReportRejected = "REPORT_REJECTED";
    public const string ReportResolved = "REPORT_RESOLVED";
    public const string RoadRiskAlert = "ROAD_RISK_ALERT";
    public const string System = "SYSTEM";
}

public static class NotificationReadFilters
{
    public const string All = "all";
    public const string Unread = "unread";
    public const string Read = "read";

    public static string Normalize(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        Unread => Unread,
        Read => Read,
        _ => All
    };
}
