using System;
using System.Collections.Generic;

namespace Jellyfin.Plugin.UserManagement.Models;

/// <summary>
/// Per invite runtime counters, kept in the standalone status file rather than the configuration so
/// signups and wrong PIN attempts never rewrite the config XML.
/// </summary>
public class InviteStatus
{
    /// <summary>Gets or sets how many accounts have been created with the invite.</summary>
    public int UsedCount { get; set; }

    /// <summary>Gets or sets the count of consecutive wrong PIN attempts (resets on success).</summary>
    public int FailedPinAttempts { get; set; }

    /// <summary>Gets or sets the UTC timestamps of recent successful redemptions, used for rate limiting.</summary>
    public List<DateTime> RecentRedemptions { get; set; } = new();
}

/// <summary>
/// The on disk shape of the invite status store, keyed by invite id.
/// </summary>
public class InviteStatusData
{
    /// <summary>Gets or sets the per invite status entries.</summary>
    public Dictionary<Guid, InviteStatus> Invites { get; set; } = new();
}
