using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SafePathBD.Web.Models.ViewModels.Profile;
using SafePathBD.Web.Security;
using SafePathBD.Web.Services.Interfaces;

namespace SafePathBD.Web.Controllers;

[Authorize]
public class DashboardController : Controller
{
    private readonly IUserService _userService;
    private readonly IReportService _reportService;

    public DashboardController(IUserService userService, IReportService reportService)
    {
        _userService = userService;
        _reportService = reportService;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();

        var profile = await _userService.GetProfileAsync(userId, cancellationToken);
        if (profile is null)
        {
            return Forbid();
        }

        var stats = await _reportService.GetMyReportStatsAsync(userId, cancellationToken);
        var recent = await _reportService.GetRecentReportsForUserAsync(userId, 4, cancellationToken);

        return View(new DashboardViewModel
        {
            Profile = new ProfileViewModel
            {
                FullName = profile.FullName,
                Email = profile.Email,
                Phone = profile.Phone,
                IsActive = profile.IsActive,
                EmailVerified = profile.EmailVerified,
                JoinedAt = profile.CreatedAt,
                LastLoginAt = profile.LastLoginAt,
                Roles = profile.Roles
            },
            ReportStats = stats,
            RecentReports = recent
        });
    }
}
