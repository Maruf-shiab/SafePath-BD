using SafePathBD.Web.Models.DTOs.Dashboard;

namespace SafePathBD.Web.Services.Interfaces;

public interface IDashboardService
{
    Task<UserDashboardDto> GetUserDashboardAsync(ulong userId, CancellationToken cancellationToken = default);

    Task<ModeratorDashboardDto> GetModeratorDashboardAsync(CancellationToken cancellationToken = default);

    Task<AdminDashboardDto> GetAdminDashboardAsync(CancellationToken cancellationToken = default);
}
