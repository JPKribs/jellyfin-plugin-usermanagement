using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Data;
using Jellyfin.Data.Enums;
using Jellyfin.Database.Implementations.Entities;
using Jellyfin.Plugin.UserManagement.Services;
using Jellyfin.Plugin.UserManagement.Utilities;
using Jellyfin.Database.Implementations.Enums;
using Jellyfin.Plugin.UserManagement.Models;
using JPKribs.Jellyfin.Base;
using MediaBrowser.Controller;
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

    // The web client's home screen settings live under a fixed display-preferences item id (the
    // deterministic hash of "usersettings") for the "emby" client, shared by every user.
    private static readonly Guid HomeSettingsItemId = Guid.Parse("3CE5B65D-E116-D731-65D1-EFC4A30EC35C");
    private const string HomeSettingsClient = "emby";

    private readonly IUserManager _userManager;
    private readonly IDisplayPreferencesManager _displayPreferences;
    private readonly ActivityLogger _activity;
    private readonly ILogger<GroupService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="GroupService"/> class.
    /// </summary>
    public GroupService(
        IUserManager userManager,
        IDisplayPreferencesManager displayPreferences,
        ActivityLogger activity,
        ILogger<GroupService> logger)
    {
        _userManager = userManager;
        _displayPreferences = displayPreferences;
        _activity = activity;
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
            foreach (var user in _userManager.GetUsers())
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

        var dto = _userManager.GetUserDto(user, string.Empty);
        var policy = dto.Policy;
        group.Permissions.ApplyTo(policy, user.Id);

        if (group.Permissions.ManageIsDisabled && !group.Permissions.IsDisabled && IsLifecycleDisabled(dto, group))
        {
            policy.IsDisabled = true;
        }

        await _userManager.UpdatePolicyAsync(user.Id, policy).ConfigureAwait(false);

        if (group.Configuration.ManagesAnything())
        {
            var config = dto.Configuration;
            group.Configuration.ApplyTo(config);
            await _userManager.UpdateConfigurationAsync(user.Id, config).ConfigureAwait(false);
        }

        if (group.Configuration.ManageHomeSections)
        {
            ApplyHomeSections(user.Id, group.Configuration.HomeSections);
        }

        return true;
    }

    /// <summary>
    /// Writes the group's home screen section order onto the member's web-client display preferences.
    /// Only the "emby" (web) client is templated; other clients keep their own layout.
    /// </summary>
    private void ApplyHomeSections(Guid userId, List<string> sections)
    {
        try
        {
            var prefs = _displayPreferences.GetDisplayPreferences(userId, HomeSettingsItemId, HomeSettingsClient);
            prefs.HomeSections.Clear();
            for (var i = 0; i < sections.Count; i++)
            {
                if (Enum.TryParse<HomeSectionType>(sections[i], ignoreCase: true, out var type))
                {
                    prefs.HomeSections.Add(new HomeSection { Order = i, Type = type });
                }
            }

            _displayPreferences.UpdateDisplayPreferences(prefs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to apply home screen sections for {UserId}", userId);
        }
    }

    /// <summary>
    /// Whether a member should currently stay disabled due to the group's lifecycle rules, so a group
    /// that force-enables its members does not re-enable someone the expiry/inactivity task disabled.
    /// </summary>
    private static bool IsLifecycleDisabled(MediaBrowser.Model.Dto.UserDto dto, GroupDefinition group)
    {
        if (group.ExpiresOn is { } due && due.Date <= DateTime.UtcNow.Date)
        {
            return true;
        }

        if (group.DisableInactiveUsers && group.InactiveDays > 0)
        {
            var lastActive = dto.LastActivityDate ?? dto.LastLoginDate;
            if (lastActive is { } last && last < DateTime.UtcNow.AddDays(-group.InactiveDays))
            {
                return true;
            }
        }

        return false;
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

        foreach (var user in _userManager.GetUsers())
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

        // Only users on the built-in provider can be enrolled. The rule provider verifies logins against
        // the local password hash, and an externally authenticated user (LDAP, SSO) usually has none, so
        // switching them would turn their account into a blank password login.
        if (!string.Equals(user.AuthenticationProviderId, DefaultProviderId, StringComparison.Ordinal))
        {
            _logger.LogWarning(
                "Not enrolling {UserId} in password rules: their authentication provider ({Provider}) is not the built-in default, so password rules cannot apply safely",
                user.Id,
                user.AuthenticationProviderId);
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

        await SetAuthenticationProviderAsync(user, providerId).ConfigureAwait(false);
        _activity.Log(
            "Enrolled in group password rules: " + user.Username,
            "UserManagement.PasswordRulesEnrolled",
            userId: user.Id);
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

        await SetAuthenticationProviderAsync(user, restore).ConfigureAwait(false);
        _activity.Log(
            "Removed from group password rules: " + user.Username,
            "UserManagement.PasswordRulesUnenrolled",
            userId: user.Id);
    }

    /// <summary>
    /// Switches a user's authentication provider through the policy update path, the same route the
    /// Jellyfin dashboard's own provider dropdown uses, so core applies its normal handling.
    /// </summary>
    private async Task SetAuthenticationProviderAsync(User user, string providerId)
    {
        var policy = _userManager.GetUserDto(user, string.Empty).Policy;
        policy.AuthenticationProviderId = providerId;
        await _userManager.UpdatePolicyAsync(user.Id, policy).ConfigureAwait(false);
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

    /// <summary>
    /// Disables non-admin members of any group with inactivity disabling enabled whose last activity is
    /// older than the group's threshold. Members who have never been active are left alone.
    /// </summary>
    public async Task DisableInactiveMembersAsync(CancellationToken cancellationToken)
    {
        var plugin = Plugin.Instance;
        if (plugin is null)
        {
            return;
        }

        var work = plugin.ReadConfiguration(c => c.Groups
            .Where(g => g.DisableInactiveUsers && g.InactiveDays > 0)
            .Select(g => (g.Id, g.InactiveDays, Members: g.MemberIds.ToList()))
            .ToList());

        if (work.Count == 0)
        {
            return;
        }

        var now = DateTime.UtcNow;
        foreach (var (groupId, days, members) in work)
        {
            var cutoff = now.AddDays(-days);
            foreach (var userId in members)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var user = _userManager.GetUserById(userId);
                if (user is null || AdminExemption.IsExempt(user))
                {
                    continue;
                }

                var dto = _userManager.GetUserDto(user, string.Empty);
                var lastActive = dto.LastActivityDate ?? dto.LastLoginDate;
                if (lastActive is null || lastActive >= cutoff)
                {
                    continue;
                }

                try
                {
                    if (!dto.Policy.IsDisabled)
                    {
                        dto.Policy.IsDisabled = true;
                        await _userManager.UpdatePolicyAsync(userId, dto.Policy).ConfigureAwait(false);
                        _logger.LogInformation("Disabled inactive user {UserId} from group {GroupId}", userId, groupId);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to disable inactive user {UserId} from group {GroupId}", userId, groupId);
                }
            }
        }
    }
}
