using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SafePathBD.Web.Common;
using SafePathBD.Web.Data;
using SafePathBD.Web.Integrations.Routing;
using SafePathBD.Web.Models.DTOs.Routing;
using SafePathBD.Web.Services.Interfaces;

namespace SafePathBD.Web.Services.Implementations;

/// <summary>One deterministic incident policy for all route guidance.</summary>
public static class RouteIncidentPolicy
{
    // A VERIFIED accident influences route recommendations only for two hours
    // from its actual occurrence time. If occurrence time is unavailable,
    // the report timestamp is used as the conservative fallback.
    public static readonly TimeSpan ActiveAccidentWindow = TimeSpan.FromHours(2);

    public static bool IsAccidentActive(DateTime? accidentOccurredAt, DateTime reportedAt, DateTime utcNow)
    {
        var effectiveTime = accidentOccurredAt ?? reportedAt;
        return effectiveTime >= utcNow.Subtract(ActiveAccidentWindow);
    }

    public static string ClassifyAccident(string? severityName) =>
        severityName?.Trim().ToUpperInvariant() switch
        {
            "SEVERE" or "FATAL" => RouteIncidentStates.Affected,
            _ => RouteIncidentStates.Caution
        };

    public static string ClassifyHazard(string? hazardType, string? riskLevel)
    {
        var risk = riskLevel?.Trim().ToUpperInvariant();
        var roadBlock = string.Equals(hazardType?.Trim(), "Road Block", StringComparison.OrdinalIgnoreCase);

        if (risk == HazardRiskLevels.Critical || (roadBlock && risk == HazardRiskLevels.High))
        {
            return RouteIncidentStates.Affected;
        }

        return RouteIncidentStates.Caution;
    }

    public static int Rank(string state) => state switch
    {
        RouteIncidentStates.Affected => 2,
        RouteIncidentStates.Caution => 1,
        _ => 0
    };
}

public sealed class RouteIncidentService : IRouteIncidentService
{
    private readonly SafePathDbContext _db;
    private readonly OsrmRoutingOptions _options;
    private readonly ILogger<RouteIncidentService> _logger;

    public RouteIncidentService(
        SafePathDbContext db,
        IOptions<OsrmRoutingOptions> options,
        ILogger<RouteIncidentService> logger)
    {
        _db = db;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<RouteIncidentAssessment> AssessRouteAsync(
        IReadOnlyList<RouteCoordinate> geometry,
        CancellationToken cancellationToken = default)
    {
        if (geometry is null || geometry.Count < 2)
        {
            throw new ArgumentException("A route needs at least two geometry points.", nameof(geometry));
        }

        var proximity = _options.IncidentProximityMeters;
        var utcNow = DateTime.UtcNow;
        var bounds = RouteGeometryMath.ExpandBoundingBox(
            RouteGeometryMath.RouteBoundingBoxFor(geometry), proximity);

        var minLat = (decimal)bounds.MinLat;
        var maxLat = (decimal)bounds.MaxLat;
        var minLng = (decimal)bounds.MinLng;
        var maxLng = (decimal)bounds.MaxLng;

        // Public, officially VERIFIED reports are the only source of active route guidance.
        // RESOLVED/PENDING/etc. are excluded by the status predicate and private reports never
        // enter this projection, preventing routing from becoming a privacy side channel.
        var nearby = await _db.Reports.AsNoTracking()
            .Where(r => r.IsPublic == true && r.Status.StatusCode == ReportStatusCodes.Verified)
            .Where(r => r.Location.Latitude >= minLat && r.Location.Latitude <= maxLat)
            .Where(r => r.Location.Longitude >= minLng && r.Location.Longitude <= maxLng)
            .Select(r => new
            {
                r.ReportId,
                r.ReportType,
                r.Title,
                Latitude = (double)r.Location.Latitude,
                Longitude = (double)r.Location.Longitude,
                r.Location.LandmarkName,
                r.Location.AreaName,
                r.Location.City,
                r.ReportedAt,
                AccidentOccurredAt = r.AccidentReports != null ? r.AccidentReports.AccidentOccurredAt : null,
                SeverityName = r.AccidentReports != null ? r.AccidentReports.Severity.SeverityName : null,
                AccidentType = r.AccidentReports != null ? r.AccidentReports.AccidentType.TypeName : null,
                HazardType = r.HazardReports != null ? r.HazardReports.HazardType.HazardName : null,
                RiskLevel = r.HazardReports != null ? r.HazardReports.RiskLevel : null
            })
            .ToListAsync(cancellationToken);

        var incidents = new List<RouteIncidentDto>();
        foreach (var report in nearby)
        {
            // Old accidents remain available as verified historical/map information,
            // but they no longer penalize or reroute a journey after the 2-hour window.
            // Hazards continue to be governed by their authoritative report status.
            if (report.ReportType == ReportTypes.Accident
                && !RouteIncidentPolicy.IsAccidentActive(report.AccidentOccurredAt, report.ReportedAt, utcNow))
            {
                continue;
            }

            var point = new RouteCoordinate(report.Latitude, report.Longitude);
            var distance = RouteGeometryMath.DistancePointToPolylineMeters(point, geometry);
            if (distance > proximity)
            {
                continue;
            }

            var level = report.ReportType == ReportTypes.Accident
                ? RouteIncidentPolicy.ClassifyAccident(report.SeverityName)
                : RouteIncidentPolicy.ClassifyHazard(report.HazardType, report.RiskLevel);

            var locationLabel = FirstNonEmpty(report.LandmarkName, report.AreaName, report.City);
            incidents.Add(new RouteIncidentDto(
                report.ReportId,
                report.ReportType,
                report.Title,
                report.Latitude,
                report.Longitude,
                locationLabel,
                report.AccidentType,
                report.SeverityName,
                report.RiskLevel,
                report.HazardType,
                level,
                Math.Round(distance, 1),
                report.ReportedAt));
        }

        var ordered = incidents
            .OrderByDescending(i => RouteIncidentPolicy.Rank(i.IncidentLevel))
            .ThenBy(i => i.DistanceFromRouteMeters)
            .ThenByDescending(i => i.ReportedAt)
            .ToList();

        var state = ordered.Count == 0
            ? RouteIncidentStates.Clear
            : ordered.Any(i => i.IncidentLevel == RouteIncidentStates.Affected)
                ? RouteIncidentStates.Affected
                : RouteIncidentStates.Caution;

        _logger.LogDebug("Route incident check found {Count} public verified reports; state={State}.", ordered.Count, state);
        return new RouteIncidentAssessment(state, ordered, DateTime.UtcNow);
    }

    private static string? FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
}
