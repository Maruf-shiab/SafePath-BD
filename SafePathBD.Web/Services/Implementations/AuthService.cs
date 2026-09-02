using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SafePathBD.Web.Common;
using SafePathBD.Web.Data;
using SafePathBD.Web.Models.DTOs.Auth;
using SafePathBD.Web.Models.Entities;
using SafePathBD.Web.Services.Interfaces;

namespace SafePathBD.Web.Services.Implementations;

public sealed class AuthService : IAuthService
{
    // Verified against a throwaway hash when an email is unknown, so login timing does not reveal account existence.
    private const string DummyHash =
        "AQAAAAIAAYagAAAAEHxMEr8DKNPfaK7bcpP0KSjNzsLtOMLbUEHM1D3mNvSGcOxYYnPqZ0O0FCPjIlfUDA==";

    private readonly SafePathDbContext _db;
    private readonly IPasswordHasher<Users> _passwordHasher;
    private readonly ILogger<AuthService> _logger;

    public AuthService(SafePathDbContext db, IPasswordHasher<Users> passwordHasher, ILogger<AuthService> logger)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _logger = logger;
    }

    public async Task<RegisterResult> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default)
    {
        var email = NormalizeEmail(request.Email);
        var phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim();

        if (await _db.Users.AsNoTracking().AnyAsync(u => u.Email == email, cancellationToken))
        {
            return new RegisterResult(RegisterStatus.EmailAlreadyRegistered);
        }

        if (phone is not null && await _db.Users.AsNoTracking().AnyAsync(u => u.Phone == phone, cancellationToken))
        {
            return new RegisterResult(RegisterStatus.PhoneAlreadyRegistered);
        }

        var defaultRole = await _db.Roles
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.RoleName == RoleNames.User, cancellationToken);

        if (defaultRole is null)
        {
            _logger.LogError("Default role '{RoleName}' is missing from the roles table; registration cannot continue.", RoleNames.User);
            return new RegisterResult(RegisterStatus.DefaultRoleMissing);
        }

        var user = new Users
        {
            FullName = request.FullName.Trim(),
            Email = email,
            Phone = phone,
            IsActive = true,
            EmailVerified = false
        };

        user.PasswordHash = _passwordHasher.HashPassword(user, request.Password);

        // The user row and its default role assignment must both exist, or neither should.
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            _db.Users.Add(user);
            await _db.SaveChangesAsync(cancellationToken);

            _db.UserRoles.Add(new UserRoles { UserId = user.UserId, RoleId = defaultRole.RoleId });
            await _db.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "Registration failed while persisting the new account.");

            // A concurrent registration can still win the race against the checks above.
            return new RegisterResult(RegisterStatus.EmailAlreadyRegistered);
        }

        return new RegisterResult(RegisterStatus.Success, user.UserId);
    }

    public async Task<LoginResult> ValidateCredentialsAsync(string email, string password, CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeEmail(email);

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == normalized, cancellationToken);
        if (user is null)
        {
            _passwordHasher.VerifyHashedPassword(new Users(), DummyHash, password);
            return new LoginResult(LoginStatus.InvalidCredentials);
        }

        if (user.IsActive != true)
        {
            return new LoginResult(LoginStatus.AccountDisabled);
        }

        var verification = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, password);
        if (verification == PasswordVerificationResult.Failed)
        {
            return new LoginResult(LoginStatus.InvalidCredentials);
        }

        if (verification == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = _passwordHasher.HashPassword(user, password);
            await _db.SaveChangesAsync(cancellationToken);
        }

        return new LoginResult(LoginStatus.Success, user);
    }

    public async Task<ClaimsPrincipal> BuildPrincipalAsync(Users user, CancellationToken cancellationToken = default)
    {
        var roles = await GetUserRolesAsync(user.UserId, cancellationToken);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.UserId.ToString()),
            new(ClaimTypes.Name, user.FullName),
            new(ClaimTypes.Email, user.Email)
        };

        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        return new ClaimsPrincipal(identity);
    }

    public async Task<IReadOnlyList<string>> GetUserRolesAsync(ulong userId, CancellationToken cancellationToken = default)
    {
        return await _db.UserRoles
            .AsNoTracking()
            .Where(ur => ur.UserId == userId)
            .Select(ur => ur.Role.RoleName)
            .OrderBy(name => name)
            .ToListAsync(cancellationToken);
    }

    public async Task RecordSuccessfulLoginAsync(ulong userId, CancellationToken cancellationToken = default)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.UserId == userId, cancellationToken);
        if (user is null)
        {
            return;
        }

        // Local server time, matching the CURRENT_TIMESTAMP defaults the database writes for the other columns.
        user.LastLoginAt = DateTime.Now;
        await _db.SaveChangesAsync(cancellationToken);
    }

    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();
}
