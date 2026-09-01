using Microsoft.EntityFrameworkCore;
using SafePathBD.Web.Data;
using SafePathBD.Web.Models.DTOs.Account;
using SafePathBD.Web.Services.Interfaces;

namespace SafePathBD.Web.Services.Implementations;

public sealed class UserService : IUserService
{
    private readonly SafePathDbContext _db;

    public UserService(SafePathDbContext db)
    {
        _db = db;
    }

    public async Task<UserProfileDto?> GetProfileAsync(ulong userId, CancellationToken cancellationToken = default)
    {
        return await _db.Users
            .AsNoTracking()
            .Where(u => u.UserId == userId)
            .Select(u => new UserProfileDto(
                u.UserId,
                u.FullName,
                u.Email,
                u.Phone,
                u.IsActive == true,
                u.EmailVerified,
                u.CreatedAt,
                u.LastLoginAt,
                u.UserRoles.Select(ur => ur.Role.RoleName).OrderBy(n => n).ToList()))
            .FirstOrDefaultAsync(cancellationToken);
    }
}
