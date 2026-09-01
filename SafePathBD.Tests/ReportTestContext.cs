using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using SafePathBD.Web.Common;
using SafePathBD.Web.Data;
using SafePathBD.Web.Models.Entities;
using SafePathBD.Web.Services.Implementations;

namespace SafePathBD.Tests;

/// <summary>
/// In-memory SafePathDbContext seeded with the same lookup rows the real database contains,
/// wired to the report services under test.
/// </summary>
internal sealed class ReportTestContext : IDisposable
{
    public ReportTestContext(bool seedStatuses = true)
    {
        var options = new DbContextOptionsBuilder<SafePathDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        Db = new SafePathDbContext(options);

        if (seedStatuses)
        {
            // The same seven status codes the real report_statuses table contains.
            Db.ReportStatuses.AddRange(
                new ReportStatuses { StatusId = 1, StatusCode = ReportStatusCodes.Pending, StatusName = "Pending" },
                new ReportStatuses { StatusId = 2, StatusCode = ReportStatusCodes.UnderReview, StatusName = "Under Review" },
                new ReportStatuses { StatusId = 3, StatusCode = ReportStatusCodes.Verified, StatusName = "Verified" },
                new ReportStatuses { StatusId = 4, StatusCode = ReportStatusCodes.Rejected, StatusName = "Rejected", IsClosedStatus = true },
                new ReportStatuses { StatusId = 5, StatusCode = ReportStatusCodes.Resolved, StatusName = "Resolved", IsClosedStatus = true },
                new ReportStatuses { StatusId = 6, StatusCode = ReportStatusCodes.Duplicate, StatusName = "Duplicate", IsClosedStatus = true },
                new ReportStatuses { StatusId = 7, StatusCode = ReportStatusCodes.NeedsInfo, StatusName = "Needs More Information" });
        }

        Db.AccidentTypes.Add(new AccidentTypes { AccidentTypeId = 1, TypeName = "Vehicle Collision", IsActive = true, DefaultRiskWeight = 2m });
        Db.AccidentSeverities.Add(new AccidentSeverities { SeverityId = 2, SeverityName = "Moderate", RiskWeight = 2m });
        Db.HazardTypes.Add(new HazardTypes { HazardTypeId = 1, HazardName = "Pothole", IsActive = true, DefaultRiskWeight = 1m });
        Db.Users.Add(new Users { UserId = 7, FullName = "Reporter One", Email = "reporter@example.com", PasswordHash = "x", IsActive = true });
        Db.Users.Add(new Users { UserId = 8, FullName = "Reporter Two", Email = "other@example.com", PasswordHash = "x", IsActive = true });
        Db.Users.Add(new Users { UserId = 9, FullName = "Mod Erator", Email = "mod@example.com", PasswordHash = "x", IsActive = true });
        Db.Roles.Add(new Roles { RoleId = 2, RoleName = RoleNames.Moderator });
        Db.UserRoles.Add(new UserRoles { UserId = 9, RoleId = 2 });
        Db.SaveChanges();

        var locationService = new LocationService(Db);
        Accidents = new AccidentReportService(Db, locationService, NullLogger<AccidentReportService>.Instance);
        Hazards = new HazardReportService(Db, locationService, NullLogger<HazardReportService>.Instance);
        Reports = new ReportService(Db);
        Locations = locationService;
        Community = new ReportCommunityService(Db);
        Moderation = new ReportModerationService(Db, Reports, NullLogger<ReportModerationService>.Instance);
    }

    public SafePathDbContext Db { get; }

    public AccidentReportService Accidents { get; }

    public HazardReportService Hazards { get; }

    public ReportService Reports { get; }

    public LocationService Locations { get; }

    public ReportCommunityService Community { get; }

    public ReportModerationService Moderation { get; }

    /// <summary>Marks an existing report verified so public-visibility rules can be exercised.</summary>
    public void SetStatus(ulong reportId, string statusCode, bool isPublic = true)
    {
        var report = Db.Reports.Single(r => r.ReportId == reportId);
        report.StatusId = Db.ReportStatuses.Single(s => s.StatusCode == statusCode).StatusId;
        report.IsPublic = isPublic;
        Db.SaveChanges();
    }

    public void Dispose() => Db.Dispose();
}
