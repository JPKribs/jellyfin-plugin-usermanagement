using System;
using System.Collections.Generic;
using Jellyfin.Plugin.UserManagement.Models;
using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.UserManagement.Configuration;

/// <summary>
/// Single configuration object for the plugin. XML-serialized by Jellyfin.
/// </summary>
public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>Gets or sets the group new users are automatically placed in, or null for none.</summary>
    public Guid? DefaultGroupId { get; set; }

    /// <summary>
    /// Gets or sets an external base URL used when building invite links (e.g. https://jellyfin.example.com).
    /// When empty, the server's own address is used.
    /// </summary>
    public string InviteBaseUrl { get; set; } = string.Empty;

    /// <summary>Gets or sets the defined permission-template groups.</summary>
    public List<GroupDefinition> Groups { get; set; } = new();

    /// <summary>Gets or sets the self-service signup invites.</summary>
    public List<Invite> Invites { get; set; } = new();

    /// <summary>Gets or sets the original authentication provider for each user currently enrolled in password rules.</summary>
    public List<ProviderEnrollment> ProviderEnrollments { get; set; } = new();

    /// <summary>Gets or sets the number of wrong PIN attempts before an invite locks itself.</summary>
    public int MaxPinAttempts { get; set; } = 5;

    /// <summary>Gets or sets the number of redemptions allowed per invite within the rate-limit window. 0 disables rate limiting.</summary>
    public int InviteRateLimitCount { get; set; }

    /// <summary>Gets or sets the rate-limit window in minutes. 0 disables rate limiting.</summary>
    public int InviteRateLimitWindowMinutes { get; set; }
}
