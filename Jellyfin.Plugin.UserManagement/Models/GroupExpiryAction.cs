namespace Jellyfin.Plugin.UserManagement.Models;

/// <summary>
/// What happens to a group's members once the group's expiry date passes.
/// </summary>
public enum GroupExpiryAction
{
    /// <summary>Disable the member accounts (reversible).</summary>
    Disable,

    /// <summary>Permanently delete the member accounts (irreversible).</summary>
    Delete
}
