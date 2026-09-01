namespace SafePathBD.Web.Models.DTOs.Auth;

public sealed record RegisterRequest(string FullName, string Email, string? Phone, string Password);

public enum RegisterStatus
{
    Success,
    EmailAlreadyRegistered,
    PhoneAlreadyRegistered,
    DefaultRoleMissing
}

public sealed record RegisterResult(RegisterStatus Status, ulong UserId = 0)
{
    public bool Succeeded => Status == RegisterStatus.Success;
}
