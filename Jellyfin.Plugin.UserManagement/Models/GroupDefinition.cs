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
}
