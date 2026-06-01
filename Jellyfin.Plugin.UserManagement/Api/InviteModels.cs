using System;

namespace Jellyfin.Plugin.UserManagement.Api;

/// <summary>Admin request to create an invite.</summary>
public class CreateInviteRequest
{
    /// <summary>Gets or sets an admin-facing label.</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>Gets or sets the PIN required to redeem (empty for none).</summary>
    public string Pin { get; set; } = string.Empty;

    /// <summary>Gets or sets the group new accounts are placed in, or null.</summary>
    public Guid? GroupId { get; set; }

    /// <summary>Gets or sets when the invite expires, or null for never.</summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>Gets or sets the maximum number of accounts (0 = unlimited).</summary>
    public int MaxUses { get; set; }
}

/// <summary>The exact set of users that should be enrolled in password-rule enforcement.</summary>
public class PasswordEnrollmentRequest
{
    /// <summary>Gets or sets the user IDs to enroll (all others currently enrolled are reverted).</summary>
    public System.Collections.Generic.List<System.Guid> UserIds { get; set; } = new();
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
