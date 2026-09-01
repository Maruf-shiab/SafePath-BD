using System.Security.Claims;
using SafePathBD.Web.Models.DTOs.Auth;
using SafePathBD.Web.Models.Entities;

namespace SafePathBD.Web.Services.Interfaces;

public interface IAuthService
{
    Task<RegisterResult> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default);

    Task<LoginResult> ValidateCredentialsAsync(string email, string password, CancellationToken cancellationToken = default);

    Task<ClaimsPrincipal> BuildPrincipalAsync(Users user, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> GetUserRolesAsync(ulong userId, CancellationToken cancellationToken = default);

    Task RecordSuccessfulLoginAsync(ulong userId, CancellationToken cancellationToken = default);
}
