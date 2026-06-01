using System;

namespace Jellyfin.Plugin.UserManagement.Models;

/// <summary>
/// A self-service signup invite. Anyone with the link (and the PIN) can create one account until
/// the invite expires or its usage limit is reached.
/// </summary>
public class Invite
{
    /// <summary>Gets or sets the stable identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>Gets or sets the high-entropy URL token that identifies this invite.</summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>Gets or sets an admin-facing label.</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the salted hash of the PIN required to redeem (Jellyfin PasswordHash format).
    /// Empty means no PIN. The plaintext PIN is never stored.
    /// </summary>
    public string PinHash { get; set; } = string.Empty;

    /// <summary>Gets or sets the group new accounts are placed in, or null for none.</summary>
    public Guid? GroupId { get; set; }

    /// <summary>Gets or sets the moment the invite stops working. Null = never expires.</summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>Gets or sets the maximum number of accounts that may be created. 0 = unlimited.</summary>
    public int MaxUses { get; set; }

    /// <summary>Gets or sets how many accounts have been created with this invite.</summary>
    public int UsedCount { get; set; }

    /// <summary>Gets or sets a value indicating whether the invite is active.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Gets or sets the count of consecutive wrong PIN attempts (resets on success).</summary>
    public int FailedPinAttempts { get; set; }

    /// <summary>Gets or sets when the invite was created (UTC).</summary>
    public DateTime CreatedAt { get; set; }
}
