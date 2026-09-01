namespace SafePathBD.Web.Models.ViewModels.Profile;

public class ProfileViewModel
{
    public string FullName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string? Phone { get; set; }

    public bool IsActive { get; set; }

    public bool EmailVerified { get; set; }

    public DateTime JoinedAt { get; set; }

    public DateTime? LastLoginAt { get; set; }

    public IReadOnlyList<string> Roles { get; set; } = Array.Empty<string>();

    public string Initials =>
        string.Concat(FullName
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Take(2)
                .Select(part => char.ToUpperInvariant(part[0])));
}
