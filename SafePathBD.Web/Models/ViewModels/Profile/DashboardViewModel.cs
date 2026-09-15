using SafePathBD.Web.Models.DTOs.Dashboard;

namespace SafePathBD.Web.Models.ViewModels.Profile;

public class DashboardViewModel
{
    public ProfileViewModel Profile { get; init; } = new();

    public UserDashboardDto Dashboard { get; init; } = new(
        0, 0, 0, 0, 0, 0, 0, 0, 0,
        Array.Empty<SafePathBD.Web.Models.DTOs.Reports.ReportSummaryDto>(),
        Array.Empty<NeedsInfoReportDto>(),
        Array.Empty<SafePathBD.Web.Models.DTOs.Notifications.NotificationItemDto>(),
        Array.Empty<UserActivityItemDto>());
}
