using System;
using System.Collections.Generic;
using Jellyfin.Plugin.UserManagement.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.UserManagement;

/// <summary>
/// Main plugin entry point for User Management.
/// </summary>
public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    private readonly ILogger<Plugin> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="Plugin"/> class.
    /// </summary>
    public Plugin(
        IApplicationPaths applicationPaths,
        IXmlSerializer xmlSerializer,
        ILogger<Plugin> logger)
        : base(applicationPaths, xmlSerializer)
    {
        Instance = this;
        _logger = logger;

        _logger.LogInformation("User Management plugin initialized");
    }

    /// <inheritdoc />
    public override string Name => "User Management";

    /// <inheritdoc />
    public override Guid Id => Guid.Parse("670167bd-e7f8-4549-98e2-5ab2e11bc89f");

    /// <inheritdoc />
    public override string Description => "Group policy templates, account lifecycle, and password hygiene for existing Jellyfin users.";

    /// <summary>
    /// Gets the current plugin instance.
    /// </summary>
    public static Plugin? Instance { get; private set; }

    // Serializes all configuration access so concurrent writers (invite redemption, the user-created
    // event consumer, scheduled tasks) can't mutate/serialize the same collections at once.
    private static readonly object ConfigLock = new();

    /// <summary>
    /// Atomically mutates and (optionally) persists the configuration under a process-wide lock.
    /// </summary>
    /// <param name="mutate">Mutation to apply; return <c>true</c> to persist the change.</param>
    public void MutateConfiguration(Func<PluginConfiguration, bool> mutate)
    {
        ArgumentNullException.ThrowIfNull(mutate);
        lock (ConfigLock)
        {
            if (mutate(Configuration))
            {
                SaveConfiguration();
            }
        }
    }

    /// <summary>
    /// Reads from the configuration under the same lock (safe against concurrent mutation).
    /// </summary>
    /// <typeparam name="T">The read result type.</typeparam>
    /// <param name="read">The read projection.</param>
    /// <returns>The projected value.</returns>
    public T ReadConfiguration<T>(Func<PluginConfiguration, T> read)
    {
        ArgumentNullException.ThrowIfNull(read);
        lock (ConfigLock)
        {
            return read(Configuration);
        }
    }

    /// <inheritdoc />
    public IEnumerable<PluginPageInfo> GetPages()
    {
        var ns = typeof(Plugin).Namespace;

        // Groups page (anchor feature, shown in the main menu)
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

        // Invites page
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

        // Settings page
        yield return new PluginPageInfo
        {
            Name = "usermanagement_settings",
            EmbeddedResourcePath = $"{ns}.Configuration.usermanagement_settings.html"
        };

        yield return new PluginPageInfo
        {
            Name = "usermanagement_settings.js",
            EmbeddedResourcePath = $"{ns}.Configuration.usermanagement_settings.js"
        };

        // Shared resources
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
    }
}
