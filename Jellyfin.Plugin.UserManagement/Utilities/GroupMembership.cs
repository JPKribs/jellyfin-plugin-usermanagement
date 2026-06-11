using System;
using System.Collections.Generic;
using Jellyfin.Plugin.UserManagement.Models;

namespace Jellyfin.Plugin.UserManagement.Utilities;

/// <summary>
/// Enforces the single membership model on group definitions. A user belongs to at most one group.
/// The dashboard keeps this invariant in the browser, but configuration can arrive from any API
/// client, so it is enforced again before anything is persisted.
/// </summary>
public static class GroupMembership
{
    /// <summary>
    /// Removes duplicate memberships in place, both across groups and within a single group. When a
    /// user appears in several groups, the first group in list order keeps them, matching the order
    /// the sync pass applies groups in.
    /// </summary>
    /// <param name="groups">The group definitions to normalize.</param>
    /// <returns>The number of memberships removed.</returns>
    public static int EnforceSingleMembership(IEnumerable<GroupDefinition> groups)
    {
        ArgumentNullException.ThrowIfNull(groups);

        var seen = new HashSet<Guid>();
        var removed = 0;
        foreach (var group in groups)
        {
            var kept = new List<Guid>(group.MemberIds.Count);
            foreach (var id in group.MemberIds)
            {
                if (seen.Add(id))
                {
                    kept.Add(id);
                }
                else
                {
                    removed++;
                }
            }

            if (kept.Count != group.MemberIds.Count)
            {
                group.MemberIds.Clear();
                group.MemberIds.AddRange(kept);
            }
        }

        return removed;
    }
}
