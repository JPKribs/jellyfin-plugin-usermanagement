using System;
using System.Collections.Generic;
using System.Linq;
using Jellyfin.Database.Implementations.Entities;
using Jellyfin.Plugin.UserManagement.Configuration;
using Jellyfin.Plugin.UserManagement.Models;
using Jellyfin.Plugin.UserManagement.Services;
using Jellyfin.Plugin.UserManagement.Utilities;
using JPKribs.Jellyfin.Base;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.UserManagement;

/// <summary>
/// Main plugin entry point for User Management.
/// </summary>
public class Plugin : PluginBase<Plugin, PluginConfiguration>
{
    private readonly ILogger<Plugin> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="Plugin"/> class.
    /// </summary>
    /// <param name="applicationPaths">The application paths.</param>
    /// <param name="xmlSerializer">The XML serializer.</param>
    /// <param name="logger">The logger.</param>
    public Plugin(
        IApplicationPaths applicationPaths,
        IXmlSerializer xmlSerializer,
        ILogger<Plugin> logger)
        : base(applicationPaths, xmlSerializer)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
        logger.LogInformation("User Management plugin initialized");
    }

    /// <inheritdoc />
    public override string Name => "User Management";

    /// <inheritdoc />
    public override Guid Id => Guid.Parse("670167bd-e7f8-4549-98e2-5ab2e11bc89f");

    /// <inheritdoc />
    public override string Description => "Group policy templates, account lifecycle, and password hygiene for existing Jellyfin users.";

    /// <summary>
    /// Normalizes incoming configuration before it is persisted, and audits group creation and deletion.
    /// </summary>
    /// <param name="configuration">The incoming configuration.</param>
    public override void UpdateConfiguration(BasePluginConfiguration configuration)
    {
        if (configuration is not PluginConfiguration config)
        {
            base.UpdateConfiguration(configuration);
            return;
        }

        // MutateConfiguration is the only handle on the base class's configuration lock, and the swap
        // has to happen under it: an internal write in flight (an invite redemption adding a member)
        // targets the live configuration object, which this save is about to replace. The lambda
        // returns false because the swap persists the incoming configuration itself.
        MutateConfiguration(_ =>
        {
            LogGroupChanges(config);
            Normalize(config);
            base.UpdateConfiguration(config);
            return false;
        });
    }

    /// <summary>
    /// Brings a configuration back to the invariants the plugin relies on: one group per user, no
    /// administrators or deleted accounts in a membership list, a default group that still exists, and
    /// no enabled invites pointing at a group that disallows all password changes. Called on every save,
    /// including the internal ones, so a write from any path lands normalized.
    /// </summary>
    /// <param name="config">The configuration to normalize in place.</param>
    /// <param name="keepMember">
    /// A user id to keep regardless of whether the server reports them yet, for the callers that add a
    /// member in the same write. An account created moments ago is the one case where "not in the user
    /// list" would mean "too early to see it" rather than "deleted".
    /// </param>
    /// <returns><c>true</c> when something had to be corrected.</returns>
    public bool Normalize(PluginConfiguration config, Guid? keepMember = null)
    {
        ArgumentNullException.ThrowIfNull(config);

        var changed = GroupMembership.EnforceSingleMembership(config.Groups) > 0;

        if (CurrentUsers() is { } users)
        {
            var known = users.Select(u => u.Id).ToHashSet();
            var admins = users.Where(AdminExemption.IsExempt).Select(u => u.Id).ToHashSet();
            if (keepMember is { } keep)
            {
                known.Add(keep);
            }

            // Groups never touch an administrator's policy, so a member who has been promoted is only
            // dead weight in the list. Their enrollment record stays: the enrollment reconcile needs it
            // to put them back on the authentication provider they had before.
            var promoted = GroupMembership.RemoveMembers(config.Groups, admins.Contains);
            if (promoted.Count > 0)
            {
                changed = true;
                _logger.LogInformation("Removed {Count} administrator(s) from group membership", promoted.Count);
            }

            var deleted = GroupMembership.RemoveMembers(config.Groups, id => !known.Contains(id));
            var orphanedEnrollments = config.ProviderEnrollments.RemoveAll(e => !known.Contains(e.UserId));
            if (deleted.Count > 0 || orphanedEnrollments > 0)
            {
                changed = true;
                _logger.LogInformation(
                    "Dropped {Members} membership(s) and {Enrollments} enrollment record(s) for deleted users",
                    deleted.Count,
                    orphanedEnrollments);
            }
        }

        if (config.DefaultGroupId is { } defaultId && !config.Groups.Any(g => g.Id.Equals(defaultId)))
        {
            config.DefaultGroupId = null;
            changed = true;
        }

        return InviteService.DisableInvitesForBlockedGroups(config) > 0 || changed;
    }

    /// <summary>
    /// The server's current users, or null when the list cannot be read. An empty list is treated as
    /// unreadable rather than as "every user is gone", so a normalize pass can never wipe membership.
    /// </summary>
    private List<User>? CurrentUsers()
    {
        var manager = UserManagerAccessor.Instance;
        if (manager is null)
        {
            return null;
        }

        try
        {
            var users = manager.GetUsers().ToList();
            return users.Count > 0 ? users : null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not read the user list; skipping membership cleanup");
            return null;
        }
    }

    /// <summary>
    /// Writes an activity entry for each group added or removed by an incoming save. Groups are created
    /// and deleted through the generic configuration endpoint, so this id diff is the only place either
    /// can be observed for auditing.
    /// </summary>
    /// <param name="config">The incoming configuration.</param>
    private void LogGroupChanges(PluginConfiguration config)
    {
        // Configuration can be null on the very first save, before any file exists.
        var existing = new HashSet<Guid>(Configuration?.Groups.Select(g => g.Id) ?? Enumerable.Empty<Guid>());
        foreach (var group in config.Groups.Where(g => !existing.Contains(g.Id)))
        {
            ActivityLoggerAccessor.Instance?.Log(
                "Group created: " + group.Name,
                "UserManagement.GroupCreated");
        }

        var incoming = new HashSet<Guid>(config.Groups.Select(g => g.Id));
        foreach (var group in (Configuration?.Groups ?? Enumerable.Empty<GroupDefinition>()).Where(g => !incoming.Contains(g.Id)))
        {
            ActivityLoggerAccessor.Instance?.Log(
                "Group deleted: " + group.Name,
                "UserManagement.GroupDeleted");
        }
    }

    /// <inheritdoc />
    public override IEnumerable<PluginPageInfo> GetPages()
    {
        var ns = typeof(Plugin).Namespace;

        yield return new PluginPageInfo
        {
            Name = "usermanagement_groups",
            EmbeddedResourcePath = $"{ns}.Configuration.usermanagement_groups.html",
            MenuSection = "server",
            DisplayName = "User Management",
            EnableInMainMenu = true,
            MenuIcon = "manage_accounts"
        };

        yield return new PluginPageInfo
        {
            Name = "usermanagement_groups.js",
            EmbeddedResourcePath = $"{ns}.Configuration.usermanagement_groups.js"
        };

        yield return new PluginPageInfo
        {
            Name = "usermanagement_invites",
            EmbeddedResourcePath = $"{ns}.Configuration.usermanagement_invites.html"
        };

        yield return new PluginPageInfo
        {
            Name = "usermanagement_invites.js",
            EmbeddedResourcePath = $"{ns}.Configuration.usermanagement_invites.js"
        };

        yield return new PluginPageInfo
        {
            Name = "usermanagement_resets",
            EmbeddedResourcePath = $"{ns}.Configuration.usermanagement_resets.html"
        };

        yield return new PluginPageInfo
        {
            Name = "usermanagement_resets.js",
            EmbeddedResourcePath = $"{ns}.Configuration.usermanagement_resets.js"
        };

        yield return new PluginPageInfo
        {
            Name = "usermanagement_shared.css",
            EmbeddedResourcePath = $"{ns}.Configuration.usermanagement_shared.css"
        };

        yield return new PluginPageInfo
        {
            Name = "usermanagement_shared.js",
            EmbeddedResourcePath = $"{ns}.Configuration.usermanagement_shared.js"
        };

        foreach (var page in GetSharedPages("usermanagement"))
        {
            yield return page;
        }
    }
}
