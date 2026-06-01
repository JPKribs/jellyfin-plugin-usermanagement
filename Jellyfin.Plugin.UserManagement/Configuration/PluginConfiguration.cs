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

    /// <summary>Gets or sets the number of wrong PIN attempts before an invite locks itself.</summary>
    public int MaxPinAttempts { get; set; } = 5;

    // ===== Password requirements =====

    /// <summary>Gets or sets a value indicating whether an empty / no password is disallowed.</summary>
    public bool PasswordDisallowEmpty { get; set; }

    /// <summary>Gets or sets the minimum password length (applied to invite signups, and to resets when enforced).</summary>
    public int PasswordMinLength { get; set; } = 8;

    /// <summary>Gets or sets a value indicating whether a capital letter is required.</summary>
    public bool PasswordRequireUppercase { get; set; }

    /// <summary>Gets or sets a value indicating whether a number is required.</summary>
    public bool PasswordRequireNumber { get; set; }

    /// <summary>Gets or sets a value indicating whether a symbol is required.</summary>
    public bool PasswordRequireSymbol { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether newly created non-admin users are automatically
    /// enrolled in password-rule enforcement.
    /// </summary>
    public bool PasswordRulesApplyToNewUsers { get; set; }
}
