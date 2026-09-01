namespace SafePathBD.Web.Models.DTOs.Account;

/// <summary>
/// Safe projection of a user account. Never carries the password hash.
/// </summary>
public sealed record UserProfileDto(
    ulong UserId,
    string FullName,
    string Email,
    string? Phone,
    bool IsActive,
    bool EmailVerified,
    DateTime CreatedAt,
    DateTime? LastLoginAt,
    IReadOnlyList<string> Roles);
