using Microsoft.AspNetCore.Mvc;
using SafePathBD.Web.Models.ViewModels.Notifications;
using SafePathBD.Web.Security;
using SafePathBD.Web.Services.Interfaces;

namespace SafePathBD.Web.ViewComponents;

public sealed class NotificationBellViewComponent : ViewComponent
{
    private readonly INotificationService _notifications;

    public NotificationBellViewComponent(INotificationService notifications)
    {
        _notifications = notifications;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        if (UserClaimsPrincipal.Identity?.IsAuthenticated != true)
        {
            return Content(string.Empty);
        }

        var count = await _notifications.GetUnreadCountAsync(
            UserClaimsPrincipal.GetUserId(), HttpContext.RequestAborted);

        return View(new NotificationBellViewModel { UnreadCount = count });
    }
}
