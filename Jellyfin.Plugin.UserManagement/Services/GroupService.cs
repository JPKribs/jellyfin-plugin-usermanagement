using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Data;
using Jellyfin.Data.Enums;
using Jellyfin.Database.Implementations.Entities;
using Jellyfin.Plugin.UserManagement.Services;
using Jellyfin.Database.Implementations.Enums;
using Jellyfin.Plugin.UserManagement.Models;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Users;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.UserManagement.Services;

/// <summary>
/// Applies <see cref="GroupDefinition"/> templates onto member <see cref="UserPolicy"/> objects,
/// honoring the Override / Inherit model: only permissions the group manages are written; the rest
/// are left exactly as the user has them.
/// </summary>
public class GroupService
{
    private const string DefaultProviderId = "Jellyfin.Server.Implementations.Users.DefaultAuthenticationProvider";

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

        var defaultGroupId = defaultGroup.Id;
        var added = 0;
        plugin.MutateConfiguration(cfg =>
        {
            var group = cfg.Groups.FirstOrDefault(g => g.Id.Equals(defaultGroupId));
            if (group is null)
            {
                return false;
            }

            var assigned = cfg.Groups.SelectMany(g => g.MemberIds).ToHashSet();
            foreach (var user in _userManager.Users)
            {
                if (user.HasPermission(PermissionKind.IsAdministrator))
                {
                    continue;
                }

                if (assigned.Add(user.Id))
                {
                    group.MemberIds.Add(user.Id);
                    added++;
                }
            }

            return added > 0;
        });

        if (added > 0)
        {
            _logger.LogInformation("Added {Count} unassigned user(s) to default group {GroupId}", added, defaultGroupId);
        }

        return added;
    }

    /// <summary>
    /// Applies a group's managed (Override) permissions to a single user. Administrators are always skipped.
    /// </summary>
    /// <returns><c>true</c> if the policy was applied; <c>false</c> if the user was skipped.</returns>
    public async Task<bool> ApplyGroupAsync(User user, GroupDefinition group)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(group);

        if (Plugin.Instance is null)
        {
            return false;
        }

        if (AdminExemption.IsExempt(user))
        {
            _logger.LogDebug("Skipping group sync for admin {UserId}", user.Id);
            return false;
        }

        var policy = _userManager.GetUserDto(user, string.Empty).Policy;
        Merge(group.Permissions, policy, user.Id);

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

        var work = plugin.ReadConfiguration(c =>
        {
            var seen = new HashSet<Guid>();
            return c.Groups
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

        await ReconcileEnrollmentAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Aligns password-rule enrollment with group membership. Enrollment is driven entirely by group
    /// membership: non-admin members of a password-enforcing group are enrolled, and anyone enrolled
    /// who is no longer in such a group is reverted to their original provider.
    /// </summary>
    public async Task ReconcileEnrollmentAsync()
    {
        var plugin = Plugin.Instance;
        if (plugin is null)
        {
            return;
        }

        var enforced = plugin.ReadConfiguration(c => c.Groups
            .Where(g => g.Password is { Enabled: true })
            .SelectMany(g => g.MemberIds)
            .ToHashSet());

        foreach (var user in _userManager.Users)
        {
            if (AdminExemption.IsExempt(user))
            {
                continue;
            }

            try
            {
                if (enforced.Contains(user.Id))
                {
                    await EnrollAsync(user).ConfigureAwait(false);
                }
                else
                {
                    await UnenrollAsync(user).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to reconcile password enrollment for {UserId}", user.Id);
            }
        }
    }

    /// <summary>
    /// Enrolls a user in password-rule enforcement, recording their original authentication provider so
    /// it can be restored later. No-op if the user is already enrolled.
    /// </summary>
    public async Task EnrollAsync(User user)
    {
        ArgumentNullException.ThrowIfNull(user);
        var plugin = Plugin.Instance;
        if (plugin is null)
        {
            return;
        }

        var providerId = typeof(PasswordRuleAuthenticationProvider).FullName!;
        if (string.Equals(user.AuthenticationProviderId, providerId, StringComparison.Ordinal))
        {
            return;
        }

        var original = user.AuthenticationProviderId;
        plugin.MutateConfiguration(cfg =>
        {
            if (cfg.ProviderEnrollments.Any(e => e.UserId.Equals(user.Id)))
            {
                return false;
            }

            cfg.ProviderEnrollments.Add(new ProviderEnrollment { UserId = user.Id, OriginalProviderId = original });
            return true;
        });

        user.AuthenticationProviderId = providerId;
        await _userManager.UpdateUserAsync(user).ConfigureAwait(false);
    }

    /// <summary>
    /// Reverts a user from password-rule enforcement back to the authentication provider they had before
    /// enrollment, falling back to the built-in default if none was recorded. No-op if not enrolled.
    /// </summary>
    public async Task UnenrollAsync(User user)
    {
        ArgumentNullException.ThrowIfNull(user);
        var plugin = Plugin.Instance;
        if (plugin is null)
        {
            return;
        }

        var providerId = typeof(PasswordRuleAuthenticationProvider).FullName!;
        if (!string.Equals(user.AuthenticationProviderId, providerId, StringComparison.Ordinal))
        {
            return;
        }

        var restore = DefaultProviderId;
        plugin.MutateConfiguration(cfg =>
        {
            var record = cfg.ProviderEnrollments.FirstOrDefault(e => e.UserId.Equals(user.Id));
            if (record is null)
            {
                return false;
            }

            if (!string.IsNullOrEmpty(record.OriginalProviderId))
            {
                restore = record.OriginalProviderId;
            }

            cfg.ProviderEnrollments.RemoveAll(e => e.UserId.Equals(user.Id));
            return true;
        });

        user.AuthenticationProviderId = restore;
        await _userManager.UpdateUserAsync(user).ConfigureAwait(false);
    }

    /// <summary>
    /// Applies each group's expiry action (disable or delete) to its non-admin members once the
    /// group's expiry date has been reached. Admins are never disabled or deleted.
    /// </summary>
    public async Task ExpireGroupsAsync(IProgress<double>? progress, CancellationToken cancellationToken)
    {
        var plugin = Plugin.Instance;
        if (plugin is null)
        {
            progress?.Report(100);
            return;
        }

        var today = DateTime.UtcNow.Date;
        var work = plugin.ReadConfiguration(c => c.Groups
            .Where(g => g.ExpiresOn is { } due && due.Date <= today)
            .Select(g => (g.Id, g.ExpiryAction, Members: g.MemberIds.ToList()))
            .ToList());

        if (work.Count == 0)
        {
            progress?.Report(100);
            return;
        }

        var total = work.Sum(w => w.Members.Count);
        if (total == 0)
        {
            progress?.Report(100);
            return;
        }

        var deleted = new List<Guid>();
        var processed = 0;
        foreach (var (groupId, action, members) in work)
        {
            foreach (var userId in members)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var user = _userManager.GetUserById(userId);
                if (user is not null && !AdminExemption.IsExempt(user))
                {
                    try
                    {
                        if (action == GroupExpiryAction.Delete)
                        {
                            await _userManager.DeleteUserAsync(userId).ConfigureAwait(false);
                            deleted.Add(userId);
                            _logger.LogInformation("Deleted expired user {UserId} from group {GroupId}", userId, groupId);
                        }
                        else
                        {
                            var policy = _userManager.GetUserDto(user, string.Empty).Policy;
                            if (!policy.IsDisabled)
                            {
                                policy.IsDisabled = true;
                                await _userManager.UpdatePolicyAsync(userId, policy).ConfigureAwait(false);
                                _logger.LogInformation("Disabled expired user {UserId} from group {GroupId}", userId, groupId);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to expire user {UserId} from group {GroupId}", userId, groupId);
                    }
                }

                processed++;
                progress?.Report(processed * 100.0 / total);
            }
        }

        if (deleted.Count > 0)
        {
            plugin.MutateConfiguration(cfg =>
            {
                foreach (var group in cfg.Groups)
                {
                    group.MemberIds.RemoveAll(id => deleted.Contains(id));
                }

                cfg.ProviderEnrollments.RemoveAll(e => deleted.Contains(e.UserId));
                return true;
            });
        }
    }

    internal static void Merge(GroupPermissions p, UserPolicy policy, Guid userId)
    {
        if (p.ManageEnableRemoteAccess)
        {
            policy.EnableRemoteAccess = p.EnableRemoteAccess;
        }

        if (p.ManageEnableCollectionManagement)
        {
            policy.EnableCollectionManagement = p.EnableCollectionManagement;
        }

        if (p.ManageEnableSubtitleManagement)
        {
            policy.EnableSubtitleManagement = p.EnableSubtitleManagement;
        }

        if (p.ManageEnableLiveTvAccess)
        {
            policy.EnableLiveTvAccess = p.EnableLiveTvAccess;
        }

        if (p.ManageEnableLiveTvManagement)
        {
            policy.EnableLiveTvManagement = p.EnableLiveTvManagement;
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

        if (p.ManageRemoteClientBitrateLimit)
        {
            policy.RemoteClientBitrateLimit = p.RemoteClientBitrateLimit;
        }

        if (p.ManageSyncPlayAccess
            && Enum.TryParse<SyncPlayUserAccessType>(p.SyncPlayAccess, out var access))
        {
            policy.SyncPlayAccess = access;
        }

        if (p.ManageEnableContentDeletion)
        {
            policy.EnableContentDeletion = p.EnableContentDeletion;
            policy.EnableContentDeletionFromFolders = p.EnableContentDeletionFromFolders.ToArray();
        }

        if (p.ManageEnableRemoteControlOfOtherUsers)
        {
            policy.EnableRemoteControlOfOtherUsers = p.EnableRemoteControlOfOtherUsers;
        }

        if (p.ManageEnableSharedDeviceControl)
        {
            policy.EnableSharedDeviceControl = p.EnableSharedDeviceControl;
        }

        if (p.ManageEnableContentDownloading)
        {
            policy.EnableContentDownloading = p.EnableContentDownloading;
        }

        if (p.ManageIsDisabled)
        {
            policy.IsDisabled = p.IsDisabled;
        }

        if (p.ManageIsHidden)
        {
            policy.IsHidden = p.IsHidden;
        }

        if (p.ManageLoginAttemptsBeforeLockout)
        {
            policy.LoginAttemptsBeforeLockout = p.LoginAttemptsBeforeLockout;
        }

        if (p.ManageMaxActiveSessions)
        {
            policy.MaxActiveSessions = p.MaxActiveSessions;
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

        if (p.ManageDeviceAccess)
        {
            policy.EnableAllDevices = p.EnableAllDevices;
            policy.EnabledDevices = p.EnabledDevices.ToArray();
        }

        if (p.ManageMaxParentalRating)
        {
            policy.MaxParentalRating = p.MaxParentalRating;
            policy.MaxParentalSubRating = p.MaxParentalSubRating;
        }

        if (p.ManageBlockUnratedItems)
        {
            policy.BlockUnratedItems = p.BlockUnratedItems
                .Select(name => Enum.TryParse<UnratedItem>(name, out var item) ? (UnratedItem?)item : null)
                .Where(item => item.HasValue)
                .Select(item => item!.Value)
                .ToArray();
        }

        if (p.ManageAllowedTags)
        {
            policy.AllowedTags = p.AllowedTags.ToArray();
        }

        if (p.ManageBlockedTags)
        {
            policy.BlockedTags = p.BlockedTags.ToArray();
        }

        if (p.ManageAccessSchedules)
        {
            policy.AccessSchedules = p.AccessSchedules
                .Where(s => Enum.IsDefined(typeof(DynamicDayOfWeek), s.DayOfWeek))
                .Select(s => new AccessSchedule(
                    Enum.Parse<DynamicDayOfWeek>(s.DayOfWeek),
                    s.StartHour,
                    s.EndHour,
                    userId))
                .ToArray();
        }
    }
}
