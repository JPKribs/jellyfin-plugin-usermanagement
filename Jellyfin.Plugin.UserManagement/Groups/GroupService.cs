using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Data;
using Jellyfin.Database.Implementations.Entities;
using Jellyfin.Plugin.UserManagement.Common;
using Jellyfin.Database.Implementations.Enums;
using Jellyfin.Plugin.UserManagement.Models;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Users;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.UserManagement.Groups;

/// <summary>
/// Applies <see cref="GroupDefinition"/> templates onto member <see cref="UserPolicy"/> objects,
/// honoring the Override / Inherit model: only permissions the group manages are written; the rest
/// are left exactly as the user has them.
/// </summary>
public class GroupService
{
    private readonly IUserManager _userManager;
    private readonly ILogger<GroupService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="GroupService"/> class.
    /// </summary>
    public GroupService(IUserManager userManager, ILogger<GroupService> logger)
    {
        _userManager = userManager;
        _logger = logger;
    }

    /// <summary>
    /// Finds a group definition by ID in the current configuration.
    /// </summary>
    public GroupDefinition? FindGroup(Guid id)
        => Plugin.Instance?.ReadConfiguration(c => c.Groups.FirstOrDefault(g => g.Id.Equals(id)));

    /// <summary>
    /// Gets the group new users should receive, or null if none is configured.
    /// </summary>
    public GroupDefinition? GetDefaultGroup()
        => Plugin.Instance?.ReadConfiguration(c =>
            c.DefaultGroupId is { } id ? c.Groups.FirstOrDefault(g => g.Id.Equals(id)) : null);

    /// <summary>
    /// Adds every user who is not yet a member of any group to the default group, if one is set.
    /// Membership only; permissions are pushed by the separate apply step.
    /// </summary>
    /// <returns>The number of users newly added to the default group.</returns>
    public int AssignUnassignedToDefault()
    {
        var plugin = Plugin.Instance;
        var defaultGroup = GetDefaultGroup();
        if (plugin is null || defaultGroup is null)
        {
            return 0;
        }

        var added = 0;
        plugin.MutateConfiguration(config =>
        {
            var assigned = config.Groups.SelectMany(g => g.MemberIds).ToHashSet();
            foreach (var user in _userManager.Users)
            {
                // Admins are exempt from enforcement, so never auto-assign them to a group.
                if (user.HasPermission(PermissionKind.IsAdministrator))
                {
                    continue;
                }

                if (!assigned.Contains(user.Id))
                {
                    defaultGroup.MemberIds.Add(user.Id);
                    added++;
                }
            }

            return added > 0;
        });

        if (added > 0)
        {
            _logger.LogInformation("Added {Count} unassigned user(s) to default group {GroupId}", added, defaultGroup.Id);
        }

        return added;
    }

    /// <summary>
    /// Applies a group's managed permissions to a single user. Administrators are always skipped,
    /// and the global "allow overrides" switch is honored.
    /// </summary>
    /// <returns><c>true</c> if the policy was applied; <c>false</c> if the user was skipped.</returns>
    public async Task<bool> ApplyGroupAsync(User user, GroupDefinition group)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(group);

        var config = Plugin.Instance?.Configuration;
        if (config is null)
        {
            return false;
        }

        // Administrators are never modified by group sync — their policy (including the admin flag)
        // is left untouched, which is what prevents a group from ever locking you out.
        if (AdminExemption.IsExempt(user))
        {
            _logger.LogDebug("Skipping group sync for admin {UserId}", user.Id);
            return false;
        }

        var policy = _userManager.GetUserDto(user, string.Empty).Policy;
        Merge(group.Permissions, policy);

        await _userManager.UpdatePolicyAsync(user.Id, policy).ConfigureAwait(false);
        return true;
    }

    /// <summary>
    /// Reconciles every member of every group back to that group's managed permissions.
    /// </summary>
    public async Task SyncAllAsync(IProgress<double>? progress, CancellationToken cancellationToken)
    {
        var plugin = Plugin.Instance;
        if (plugin is null)
        {
            progress?.Report(100);
            return;
        }

        // Snapshot the (group, user) work list under the config lock and dedupe by user
        // (first group wins) so a user accidentally in two groups gets a deterministic result.
        var work = plugin.ReadConfiguration(config =>
        {
            var seen = new HashSet<Guid>();
            return config.Groups
                .SelectMany(g => g.MemberIds.Select(id => (Group: g, UserId: id)))
                .Where(x => seen.Add(x.UserId))
                .ToList();
        });

        if (work.Count == 0)
        {
            progress?.Report(100);
            return;
        }

        var processed = 0;
        foreach (var (group, userId) in work)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var user = _userManager.GetUserById(userId);
            if (user is not null)
            {
                try
                {
                    await ApplyGroupAsync(user, group).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to apply group {GroupId} to user {UserId}", group.Id, userId);
                }
            }

            processed++;
            progress?.Report(processed * 100.0 / work.Count);
        }
    }

    // Overwrites only the permissions the group manages. Groups can never manage the administrator
    // flag (it isn't part of GroupPermissions), so sync can neither grant nor remove admin.
    private static void Merge(GroupPermissions p, UserPolicy policy)
    {
        if (p.ManageIsHidden)
        {
            policy.IsHidden = p.IsHidden;
        }

        if (p.ManageIsDisabled)
        {
            policy.IsDisabled = p.IsDisabled;
        }

        if (p.ManageEnableCollectionManagement)
        {
            policy.EnableCollectionManagement = p.EnableCollectionManagement;
        }

        if (p.ManageEnableSubtitleManagement)
        {
            policy.EnableSubtitleManagement = p.EnableSubtitleManagement;
        }

        if (p.ManageEnableLyricManagement)
        {
            policy.EnableLyricManagement = p.EnableLyricManagement;
        }

        if (p.ManageLibraryAccess)
        {
            policy.EnableAllFolders = p.EnableAllFolders;
            policy.EnabledFolders = p.EnabledFolders.ToArray();
        }

        if (p.ManageChannelAccess)
        {
            policy.EnableAllChannels = p.EnableAllChannels;
            policy.EnabledChannels = p.EnabledChannels.ToArray();
        }

        if (p.ManageEnableMediaPlayback)
        {
            policy.EnableMediaPlayback = p.EnableMediaPlayback;
        }

        if (p.ManageEnableAudioPlaybackTranscoding)
        {
            policy.EnableAudioPlaybackTranscoding = p.EnableAudioPlaybackTranscoding;
        }

        if (p.ManageEnableVideoPlaybackTranscoding)
        {
            policy.EnableVideoPlaybackTranscoding = p.EnableVideoPlaybackTranscoding;
        }

        if (p.ManageEnablePlaybackRemuxing)
        {
            policy.EnablePlaybackRemuxing = p.EnablePlaybackRemuxing;
        }

        if (p.ManageForceRemoteSourceTranscoding)
        {
            policy.ForceRemoteSourceTranscoding = p.ForceRemoteSourceTranscoding;
        }

        if (p.ManageEnableMediaConversion)
        {
            policy.EnableMediaConversion = p.EnableMediaConversion;
        }

        if (p.ManageEnableSyncTranscoding)
        {
            policy.EnableSyncTranscoding = p.EnableSyncTranscoding;
        }

        if (p.ManageEnableRemoteAccess)
        {
            policy.EnableRemoteAccess = p.EnableRemoteAccess;
        }

        if (p.ManageEnableRemoteControlOfOtherUsers)
        {
            policy.EnableRemoteControlOfOtherUsers = p.EnableRemoteControlOfOtherUsers;
        }

        if (p.ManageEnableSharedDeviceControl)
        {
            policy.EnableSharedDeviceControl = p.EnableSharedDeviceControl;
        }

        if (p.ManageMaxActiveSessions)
        {
            policy.MaxActiveSessions = p.MaxActiveSessions;
        }

        if (p.ManageRemoteClientBitrateLimit)
        {
            policy.RemoteClientBitrateLimit = p.RemoteClientBitrateLimit;
        }

        if (p.ManageEnableContentDownloading)
        {
            policy.EnableContentDownloading = p.EnableContentDownloading;
        }

        if (p.ManageEnableContentDeletion)
        {
            policy.EnableContentDeletion = p.EnableContentDeletion;
            policy.EnableContentDeletionFromFolders = p.EnableContentDeletionFromFolders.ToArray();
        }

        if (p.ManageEnableLiveTvAccess)
        {
            policy.EnableLiveTvAccess = p.EnableLiveTvAccess;
        }

        if (p.ManageEnableLiveTvManagement)
        {
            policy.EnableLiveTvManagement = p.EnableLiveTvManagement;
        }

        if (p.ManageEnableUserPreferenceAccess)
        {
            policy.EnableUserPreferenceAccess = p.EnableUserPreferenceAccess;
        }

        if (p.ManageEnablePublicSharing)
        {
            policy.EnablePublicSharing = p.EnablePublicSharing;
        }

        if (p.ManageSyncPlayAccess
            && Enum.TryParse<SyncPlayUserAccessType>(p.SyncPlayAccess, out var access))
        {
            policy.SyncPlayAccess = access;
        }
    }
}
