using Microsoft.AspNetCore.Mvc;
using SafePathBD.Web.Common;
using SafePathBD.Web.Services.Interfaces;

namespace SafePathBD.Web.ViewComponents;

/// <summary>
/// Navbar entry point to the review queue. Renders nothing for users without a
/// moderation role, and the badge shows the real open-queue count.
/// </summary>
public sealed class ModerationNavViewComponent : ViewComponent
{
    private readonly IReportModerationService _moderation;

    public ModerationNavViewComponent(IReportModerationService moderation)
    {
        _moderation = moderation;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        if (!UserClaimsPrincipal.IsInRole(RoleNames.Admin) && !UserClaimsPrincipal.IsInRole(RoleNames.Moderator))
        {
            return Content(string.Empty);
        }

        var counts = await _moderation.GetCountsAsync(HttpContext.RequestAborted);
        return View(counts.OpenTotal);
    }
}
