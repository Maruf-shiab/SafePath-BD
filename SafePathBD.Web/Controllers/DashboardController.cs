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
    private readonly IDashboardService _dashboardService;

    public DashboardController(IUserService userService, IDashboardService dashboardService)
    {
        _userService = userService;
        _dashboardService = dashboardService;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();

        var profile = await _userService.GetProfileAsync(userId, cancellationToken);
        if (profile is null)
        {
            return Forbid();
        }

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
            Dashboard = await _dashboardService.GetUserDashboardAsync(userId, cancellationToken)
        });
    }
}
