using System;
using Jellyfin.Plugin.UserManagement.Utilities;

namespace Jellyfin.Plugin.UserManagement.Models;

/// <summary>
/// Small derived values for <see cref="GroupDefinition"/>, kept on the model so callers read intent
/// instead of repeating the comparisons.
/// </summary>
public static class GroupDefinitionExtensions
{
    /// <summary>
    /// Returns whether the group cannot be targeted by invites. A group whose members may never set or
    /// change a password is admin managed by definition, so a self service signup into it makes no sense.
    /// </summary>
    /// <param name="group">The group, or null when an invite's group no longer resolves.</param>
    /// <returns><c>true</c> when the group's enforced password policy disallows all password changes.</returns>
    public static bool BlocksInvites(this GroupDefinition? group)
        => group?.Password is { Enabled: true, ChangeMode: PasswordChangeMode.Disallowed };

    /// <summary>
    /// Returns whether the group has reached its expiry date. Expiry is day based, matching the scheduled
    /// task that applies the expiry action, so a group is expired on or after its expiry date.
    /// </summary>
    /// <param name="group">The group, or null when it no longer resolves.</param>
    /// <returns><c>true</c> when the group has an expiry date that has been reached.</returns>
    public static bool HasExpired(this GroupDefinition? group)
        => group?.ExpiresOn is { } due && due.Date <= DateTime.UtcNow.Date;

    /// <summary>
    /// Returns a deep copy of the group, so a caller holding it outside the configuration lock never reads
    /// a list that a concurrent save is changing, and never writes into the live configuration by accident.
    /// </summary>
    /// <param name="group">The group to copy.</param>
    /// <returns>An independent copy.</returns>
    public static GroupDefinition Snapshot(this GroupDefinition group)
        => ModelSnapshot.Copy(group);
}
