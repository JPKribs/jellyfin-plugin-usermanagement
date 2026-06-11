namespace Jellyfin.Plugin.UserManagement.Models;

/// <summary>
/// The outcome of enabling or disabling an invite, so the API can tell "not found" apart from
/// "refused because the invite's group cannot take invites".
/// </summary>
public enum InviteToggleResult
{
    /// <summary>The invite was updated.</summary>
    Updated,

    /// <summary>No invite exists with the given id.</summary>
    NotFound,

    /// <summary>
    /// The invite was not enabled because its target group disallows all password changes. Disabling
    /// is always allowed.
    /// </summary>
    GroupBlocksInvites,

    /// <summary>
    /// The invite was not enabled because its expiry date has passed. Move the expiry forward instead,
    /// which re-enables it. Disabling is always allowed.
    /// </summary>
    Expired
}
