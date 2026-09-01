using System.Security.Claims;

namespace SafePathBD.Web.Security;

public static class ClaimsPrincipalExtensions
{
    public static ulong GetUserId(this ClaimsPrincipal principal)
    {
        var raw = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return ulong.TryParse(raw, out var userId) ? userId : 0;
    }

    public static string GetDisplayName(this ClaimsPrincipal principal) =>
        principal.FindFirstValue(ClaimTypes.Name) ?? "SafePath user";
}
