using System;
using System.Collections.Generic;
using Jellyfin.Plugin.UserManagement.Configuration;
using Jellyfin.Plugin.UserManagement.Models;
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

    /// <inheritdoc />
    public override IEnumerable<PluginPageInfo> GetPages()
    {
        var ns = typeof(Plugin).Namespace;

        yield return new PluginPageInfo
        {
            Name = "usermanagement_user",
            EmbeddedResourcePath = $"{ns}.Configuration.usermanagement_user.html",
            MenuSection = "server",
            DisplayName = "User Management",
            EnableInMainMenu = true
        };

        yield return new PluginPageInfo
        {
            Name = "usermanagement_user.js",
            EmbeddedResourcePath = $"{ns}.Configuration.usermanagement_user.js"
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
