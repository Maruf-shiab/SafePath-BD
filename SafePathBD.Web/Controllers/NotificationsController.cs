using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SafePathBD.Web.Common;
using SafePathBD.Web.Models.ViewModels.Notifications;
using SafePathBD.Web.Security;
using SafePathBD.Web.Services.Interfaces;

namespace SafePathBD.Web.Controllers;

[Authorize]
public sealed class NotificationsController : Controller
{
    private readonly INotificationService _notifications;

    public NotificationsController(INotificationService notifications)
    {
        _notifications = notifications;
    }

    [HttpGet("Notifications")]
    public async Task<IActionResult> Index(string? filter, int page = 1, CancellationToken cancellationToken = default)
    {
        var userId = User.GetUserId();
        var normalized = NotificationReadFilters.Normalize(filter);

        return View(new NotificationPageViewModel
        {
            Filter = normalized,
            UnreadCount = await _notifications.GetUnreadCountAsync(userId, cancellationToken),
            Notifications = await _notifications.GetPageAsync(userId, normalized, page, 20, cancellationToken)
        });
    }

    [HttpGet("api/v1/notifications/latest")]
    public async Task<IActionResult> Latest(int take = 6, CancellationToken cancellationToken = default)
    {
        var items = await _notifications.GetLatestAsync(User.GetUserId(), Math.Clamp(take, 1, 8), cancellationToken);
        return Ok(new { notifications = items });
    }

    [HttpGet("api/v1/notifications/unread-count")]
    public async Task<IActionResult> UnreadCount(CancellationToken cancellationToken = default) =>
        Ok(new { unreadCount = await _notifications.GetUnreadCountAsync(User.GetUserId(), cancellationToken) });

    [HttpPost("api/v1/notifications/{id:long}/read")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Read(long id, CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return NotFound();
        }

        var updated = await _notifications.MarkReadAsync(User.GetUserId(), (ulong)id, cancellationToken);
        return updated ? Ok(new { read = true }) : NotFound();
    }

    [HttpPost("api/v1/notifications/read-all")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReadAll(CancellationToken cancellationToken = default)
    {
        var updated = await _notifications.MarkAllReadAsync(User.GetUserId(), cancellationToken);
        return Ok(new { updated });
    }

    [HttpPost("Notifications/{id:long}/read")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReadFromPage(long id, string? returnUrl, CancellationToken cancellationToken = default)
    {
        if (id > 0)
        {
            await _notifications.MarkReadAsync(User.GetUserId(), (ulong)id, cancellationToken);
        }

        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return LocalRedirect(returnUrl);
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost("Notifications/read-all")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReadAllFromPage(string? filter, CancellationToken cancellationToken = default)
    {
        await _notifications.MarkAllReadAsync(User.GetUserId(), cancellationToken);
        TempData["NotificationMessage"] = "All notifications marked as read.";
        return RedirectToAction(nameof(Index), new { filter = NotificationReadFilters.Normalize(filter) });
    }
}
