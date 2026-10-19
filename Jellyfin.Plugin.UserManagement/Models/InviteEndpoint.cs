namespace Jellyfin.Plugin.UserManagement.Models;

/// <summary>
/// The anonymous invite endpoint a request is counted against by the per client attempt limiter.
/// </summary>
public enum InviteEndpoint
{
    /// <summary>The invite lookup behind the signup page.</summary>
    Info,

    /// <summary>The signup itself.</summary>
    Redeem
}
