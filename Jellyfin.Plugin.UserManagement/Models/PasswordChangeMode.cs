namespace Jellyfin.Plugin.UserManagement.Models;

/// <summary>
/// Whether members of a group may set or change their own password. Administrators can always change
/// a member's password, and invite signups always choose their first one, since that is part of
/// creating the account rather than a member managing an existing one.
/// </summary>
public enum PasswordChangeMode
{
    /// <summary>Members may set and change their own password, subject to the group's rules.</summary>
    Allowed,

    /// <summary>
    /// Members may set an initial password while theirs is empty, but cannot change an existing one.
    /// </summary>
    InitialOnly,

    /// <summary>Members may neither set nor change a password. Only administrators can.</summary>
    Disallowed
}
