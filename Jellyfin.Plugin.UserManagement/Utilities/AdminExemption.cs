using Jellyfin.Data;
using Jellyfin.Database.Implementations.Entities;
using Jellyfin.Database.Implementations.Enums;

namespace Jellyfin.Plugin.UserManagement.Utilities;

/// <summary>
/// Cross-cutting "is this user exempt from plugin enforcement?" check. Administrators are always
/// exempt from group enforcement — groups never modify an admin's policy, never disable or delete an
/// admin account, and never log an admin's devices out. Membership normalization drops administrators
/// from group lists on save, so a promoted member stops being enforced against on the next write.
/// </summary>
public static class AdminExemption
{
    /// <summary>
    /// Determines whether plugin-enforced restrictions should be skipped for the given user.
    /// </summary>
    /// <param name="user">The user being evaluated.</param>
    /// <returns><c>true</c> when the user is an administrator.</returns>
    public static bool IsExempt(User user)
        => user.HasPermission(PermissionKind.IsAdministrator);
}
