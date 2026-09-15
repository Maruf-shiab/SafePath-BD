using SafePathBD.Web.Common;
using SafePathBD.Web.Models.DTOs.Notifications;

namespace SafePathBD.Web.Models.ViewModels.Notifications;

public sealed class NotificationPageViewModel
{
    public string Filter { get; init; } = NotificationReadFilters.All;

    public int UnreadCount { get; init; }

    public PagedResult<NotificationItemDto> Notifications { get; init; } =
        new(Array.Empty<NotificationItemDto>(), 1, 20, 0);
}

public sealed class NotificationBellViewModel
{
    public int UnreadCount { get; init; }
}
