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
}
