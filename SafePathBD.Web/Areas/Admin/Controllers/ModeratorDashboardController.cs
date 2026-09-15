using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SafePathBD.Web.Common;
using SafePathBD.Web.Models.DTOs.Dashboard;
using SafePathBD.Web.Services.Interfaces;

namespace SafePathBD.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = RoleNames.AdminOrModerator)]
public sealed class ModeratorDashboardController : Controller
{
    private readonly IDashboardService _dashboardService;

    public ModeratorDashboardController(IDashboardService dashboardService)
    {
        _dashboardService = dashboardService;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        ViewData["ReviewerName"] = User.Identity?.Name ?? "Reviewer";
        var dashboard = await _dashboardService.GetModeratorDashboardAsync(cancellationToken);
        return View(dashboard);
    }
}
