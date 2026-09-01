using SafePathBD.Web.Models.DTOs.Account;

namespace SafePathBD.Web.Services.Interfaces;

public interface IUserService
{
    Task<UserProfileDto?> GetProfileAsync(ulong userId, CancellationToken cancellationToken = default);
}
