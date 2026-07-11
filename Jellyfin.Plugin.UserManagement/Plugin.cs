using System;
using System.Collections.Generic;
using System.Linq;
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
        logger.LogInformation("User Management plugin initialized");
    }

    /// <inheritdoc />
    public override string Name => "User Management";

    /// <inheritdoc />
    public override Guid Id => Guid.Parse("670167bd-e7f8-4549-98e2-5ab2e11bc89f");

    /// <inheritdoc />
    public override string Description => "Group policy templates, account lifecycle, and password hygiene for existing Jellyfin users.";

    /// <summary>
    /// Normalizes incoming configuration before it is persisted. The dashboard keeps each user in a
    /// single group, but configuration can arrive from any API client, so membership exclusivity is
    /// enforced here as well (the first group in list order keeps a duplicated member), and invites
    /// targeting a group that disallows all password changes are disabled.
    /// </summary>
    /// <param name="configuration">The incoming configuration.</param>
    public override void UpdateConfiguration(BasePluginConfiguration configuration)
    {
        if (configuration is PluginConfiguration config)
        {
            // Groups are created and deleted through this generic save (the dashboard has no dedicated
            // endpoint), so the id diff against the stored configuration is the only place either can
            // be observed for auditing. Configuration can be null on the very first save before any
            // file exists.
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

            GroupMembership.EnforceSingleMembership(config.Groups);
            InviteService.DisableInvitesForBlockedGroups(config);
        }

        base.UpdateConfiguration(configuration);
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
            EnableInMainMenu = true
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
