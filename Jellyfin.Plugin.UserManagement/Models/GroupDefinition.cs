using System;
using System.Collections.Generic;

namespace Jellyfin.Plugin.UserManagement.Models;

/// <summary>
/// A named permission template. Members inherit the managed permissions described by
/// <see cref="Permissions"/>; the group sync reapplies them on a schedule. A user belongs to
/// at most one group at a time.
/// </summary>
public class GroupDefinition
{
    /// <summary>Gets or sets the stable identifier for this group.</summary>
    public Guid Id { get; set; }

    /// <summary>Gets or sets the display name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the Jellyfin user IDs that belong to this group.</summary>
    public List<Guid> MemberIds { get; set; } = new();

    /// <summary>Gets or sets the permission shape applied to members.</summary>
    public GroupPermissions Permissions { get; set; } = new();

    /// <summary>Gets or sets the password complexity rules enforced on enrolled members of this group.</summary>
    public PasswordPolicy Password { get; set; } = new();

    /// <summary>
    /// Gets or sets the date this group expires, or null for never. Only the date matters (time is
    /// ignored). On or after this date the <see cref="ExpiryAction"/> is applied to every non-admin member.
    /// </summary>
    public DateTime? ExpiresOn { get; set; }

    /// <summary>Gets or sets what happens to members once the group expires.</summary>
    public GroupExpiryAction ExpiryAction { get; set; } = GroupExpiryAction.Disable;
}
