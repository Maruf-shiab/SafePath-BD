namespace SafePathBD.Web.Common;

/// <summary>
/// Values that must match the database exactly: the reports.report_type enum,
/// the report_statuses.status_code lookup and the hazard_reports.risk_level enum.
/// </summary>
public static class ReportTypes
{
    public const string Accident = "ACCIDENT";
    public const string Hazard = "HAZARD";
}

public static class ReportStatusCodes
{
    public const string Pending = "PENDING";
    public const string UnderReview = "UNDER_REVIEW";
    public const string Verified = "VERIFIED";
    public const string Rejected = "REJECTED";
    public const string Resolved = "RESOLVED";
    public const string Duplicate = "DUPLICATE";
    public const string NeedsInfo = "NEEDS_INFO";
}

public static class HazardRiskLevels
{
    public const string Low = "LOW";
    public const string Moderate = "MODERATE";
    public const string High = "HIGH";
    public const string Critical = "CRITICAL";

    public static readonly IReadOnlyList<string> All = new[] { Low, Moderate, High, Critical };

    public static bool IsValid(string? value) => value is not null && All.Contains(value);
}
