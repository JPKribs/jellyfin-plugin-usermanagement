using System.Collections.Generic;

namespace Jellyfin.Plugin.UserManagement.Models;

/// <summary>
/// One session-cleanup rule for a group: which clients it covers and how many days a device may go
/// without checking back into the server before it is logged out. When several rules cover the same
/// client, the shortest window wins.
/// </summary>
public class SessionCleanupRule
{
    /// <summary>Gets or sets which clients this rule applies to.</summary>
    public SessionCleanupClientMode Mode { get; set; } = SessionCleanupClientMode.All;

    /// <summary>Gets or sets the client names the mode includes or excludes, matched case-insensitively.</summary>
    public List<string> Clients { get; set; } = new();

    /// <summary>Gets or sets the number of days without server contact before a device is logged out.</summary>
    public int Days { get; set; } = 30;
}
