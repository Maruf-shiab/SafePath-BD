using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SafePathBD.Web.Models.ViewModels.Profile;
using SafePathBD.Web.Security;
using SafePathBD.Web.Services.Interfaces;

namespace SafePathBD.Web.Controllers;

[Authorize]
public class ProfileController : Controller
{
    private readonly IUserService _userService;

    public ProfileController(IUserService userService)
    {
        _userService = userService;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var profile = await _userService.GetProfileAsync(User.GetUserId(), cancellationToken);
        if (profile is null)
        {
            return NotFound();
        }

        return View(new ProfileViewModel
        {
            FullName = profile.FullName,
            Email = profile.Email,
            Phone = profile.Phone,
            IsActive = profile.IsActive,
            EmailVerified = profile.EmailVerified,
            JoinedAt = profile.CreatedAt,
            LastLoginAt = profile.LastLoginAt,
            Roles = profile.Roles
        });
    }
}
