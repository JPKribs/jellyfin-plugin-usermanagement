namespace Jellyfin.Plugin.UserManagement.Models;

/// <summary>
/// Which clients a <see cref="SessionCleanupRule"/> applies to.
/// </summary>
public enum SessionCleanupClientMode
{
    /// <summary>Every client is cleaned up by this rule.</summary>
    All,

    /// <summary>Only sessions on the rule's listed clients are cleaned up.</summary>
    Only,

    /// <summary>All sessions are cleaned up except sessions on the rule's listed clients.</summary>
    AllExcept
}
