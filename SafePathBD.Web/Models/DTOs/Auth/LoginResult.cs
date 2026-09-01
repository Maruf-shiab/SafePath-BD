using SafePathBD.Web.Models.Entities;

namespace SafePathBD.Web.Models.DTOs.Auth;

public enum LoginStatus
{
    Success,
    InvalidCredentials,
    AccountDisabled
}

public sealed record LoginResult(LoginStatus Status, Users? User = null)
{
    public bool Succeeded => Status == LoginStatus.Success && User is not null;
}
