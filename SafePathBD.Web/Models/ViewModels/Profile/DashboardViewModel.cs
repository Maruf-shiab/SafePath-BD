using SafePathBD.Web.Models.DTOs.Reports;

namespace SafePathBD.Web.Models.ViewModels.Profile;

public class DashboardViewModel
{
    public ProfileViewModel Profile { get; init; } = new();

    public MyReportStatsDto ReportStats { get; init; } = new(0, 0, 0, 0, 0);

    public IReadOnlyList<ReportSummaryDto> RecentReports { get; init; } = Array.Empty<ReportSummaryDto>();
}
