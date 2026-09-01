namespace SafePathBD.Web.Common;

/// <summary>
/// Role names exactly as seeded in the <c>roles</c> table.
/// </summary>
public static class RoleNames
{
    public const string Admin = "Admin";
    public const string Moderator = "Moderator";
    public const string User = "User";

    public const string AdminOrModerator = Admin + "," + Moderator;
}
