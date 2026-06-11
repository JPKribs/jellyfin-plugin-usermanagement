using System;
using System.Collections.Generic;

namespace Jellyfin.Plugin.UserManagement.Models;

/// <summary>Admin request to create an invite.</summary>
public class CreateInviteRequest
{
    /// <summary>Gets or sets an admin-facing name. Never shown to invitees.</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>Gets or sets a welcome message shown to invitees on the signup page.</summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>Gets or sets the resource links shown to the new user after signup.</summary>
    public List<InviteResource> Resources { get; set; } = new();

    /// <summary>Gets or sets the PIN required to redeem (empty for none).</summary>
    public string Pin { get; set; } = string.Empty;

    /// <summary>Gets or sets a value indicating whether new accounts go to the server's default group.</summary>
    public bool UseDefaultGroup { get; set; } = true;

    /// <summary>Gets or sets the explicit group new accounts are placed in when not using the default group.</summary>
    public Guid? GroupId { get; set; }

    /// <summary>Gets or sets when the invite expires, or null for never.</summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>Gets or sets the maximum number of accounts (0 = unlimited).</summary>
    public int MaxUses { get; set; }
}

/// <summary>Admin request to enable or disable an invite.</summary>
public class SetInviteEnabledRequest
{
    /// <summary>Gets or sets a value indicating whether the invite is enabled.</summary>
    public bool Enabled { get; set; }
}

/// <summary>Admin request to change an invite's expiry date.</summary>
public class SetInviteExpiryRequest
{
    /// <summary>Gets or sets the new expiry date, or null for never.</summary>
    public DateTime? ExpiresAt { get; set; }
}

/// <summary>Public request body to redeem an invite.</summary>
public class RedeemInviteRequest
{
    /// <summary>Gets or sets the PIN.</summary>
    public string Pin { get; set; } = string.Empty;

    /// <summary>Gets or sets the desired username.</summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>Gets or sets the desired password.</summary>
    public string Password { get; set; } = string.Empty;
}
