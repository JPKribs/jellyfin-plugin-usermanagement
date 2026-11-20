using JPKribs.Jellyfin.Base;
using MediaBrowser.Controller.Configuration;

namespace Jellyfin.Plugin.UserManagement.Services;

/// <summary>
/// Holds the server's configuration, so text the plugin writes for everyone, such as activity log entries and
/// the password change messages the web client shows, reads in the server's display language. Assigned at
/// server startup by <see cref="UserManagerAccessorInitializer"/>.
/// </summary>
public static class ServerLanguage
{
    /// <summary>Gets or sets the server's configuration, or null before the server has started.</summary>
    public static IServerConfigurationManager? Configuration { get; set; }

    /// <summary>Gets the server's display language, or null for US English before the server has started.</summary>
    public static string? Current => Configuration is { } config ? BaseTranslations.ServerLanguage(config) : null;
}
